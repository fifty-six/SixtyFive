using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Newtonsoft.Json;
using Qmmands;
using SixtyFive.Json;
using SixtyFive.Results;
using SixtyFive.Services;
using SixtyFive.Util;

namespace SixtyFive.Modules
{
    [PublicAPI]
    public class Code : DiscordModuleBase<CommandContext>
    {
        [Command("disasm", "disassemble")]
        public async Task<Result> Diassemble([Remainder] string msg)
        {
            string code = Util.Utilities.ExtractCode(msg);

            var comp = CSharpCompilation.CreateScriptCompilation
            (
                "assembly_of_suicide",
                CSharpSyntaxTree.ParseText
                (
                    code,
                    CSharpParseOptions.Default
                                      .WithKind(SourceCodeKind.Script)
                                      .WithLanguageVersion(LanguageVersion.Latest)
                ),
                Owner._refs,
                Owner._options,
                globalsType: typeof(RoslynContext<Code>)
            );

            ImmutableArray<Diagnostic> diagnostics = comp.GetDiagnostics();

            Diagnostic[] err = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();

            if (err.Length != 0)
            {
                var err_embed = new LocalEmbed
                {
                    Title = "Compilation failed.",
                    Description = string.Join("\n", err.Select(x => $"({x.Location.GetLineSpan().StartLinePosition}): [{x.Id}] {x.GetMessage()}")),
                    Color = Color.Red
                };

                return new Err(err_embed);
            }

            var ctx = new AssemblyLoadContext("sussy", true);

            try
            {
                await using var mem = new MemoryStream();

                EmitResult emit_res = comp.Emit(mem);

                if (!emit_res.Success)
                    return new Err("Compilation failed despite no diagnostics?");

                mem.Position = 0;

                Assembly asm = ctx.LoadFromStream(mem);
                
                const BindingFlags all = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

                MethodInfo? mi = asm.GetTypes().Select(x => x.GetMethod("Disassemble", all)).FirstOrDefault();

                if (mi is null)
                    return Err.AsEmbed("Unable to find entry point method `Disassemble`.");

                string res = Disassembler.DisassembleMethod(mi);

                string[] lines = res.Split("\n");

                var sb = new StringBuilder();

                foreach (string line in lines)
                {
                    if (sb.Length + line.Length < 1980)
                    {
                        sb.Append(line);
                    }
                    else
                    {
                        await Response("```x86asm\n" + sb + "```");

                        sb.Clear();

                        sb.Append(line);
                    }
                }

                if (sb.Length != 0)
                    await Response("```x86asm\n" + sb + "```");

                return new Ok();
            }
            finally
            {
                // This will take until a GC.
                ctx.Unload();
            }
        }

        [PublicAPI, Group("godbolt")]
        public class GodboltCommands : DiscordModuleBase<CommandContext>
        {
            private readonly Godbolt _bolt;

            public GodboltCommands(Godbolt godbolt) => _bolt = godbolt;

            [Command("run", "eval")]
            public async Task<Result> Run(string compiler, [Remainder] string code)
            {
                code = Util.Utilities.ExtractCode(code);

                if (!_bolt.Compilers.Contains(compiler))
                    return Err.AsEmbed("Godbolt does not contain this compiler.");

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var reqBody = new
                {
                    source = code,
                    compiler,

                    options = new
                    {
                        compilerOptions = new
                        {
                            executorRequest = true
                        },
                        filters = new
                        {
                            demangle = true,
                            directives = true,
                            execute = true,
                            intel = true,
                            labels = true
                        }
                    }
                };

                var req = new HttpRequestMessage
                {
                    RequestUri = new Uri($"https://godbolt.org/api/compiler/{compiler}/compile"),
                    Content = new StringContent(JsonConvert.SerializeObject(reqBody), Encoding.Default, "application/json"),
                    Method = HttpMethod.Post
                };

                req.Headers.Add("Accept", "application/json");

                HttpResponseMessage res = await Context.HttpClient.SendAsync(req, cts.Token);

                string str = await res.Content.ReadAsStringAsync(cts.Token);

                var obj = JsonConvert.DeserializeObject<ExecutorResponse>(str);

                if (obj is null)
                    return Err.AsEmbed("Got invalid json!");

                // Failed compilation.
                if (!obj.DidExecute)
                {
                    string output = string.Join("\n", obj.BuildResult.Stderr.Select(x => x.Text));

                    // Strip out color codes
                    output = Regex.Replace(output, "\u001b\\[.*?m", string.Empty);

                    return Err.AsCodeBlock("<Compilation Failed>\n\n" + output, "c");
                }

                var eb = new LocalEmbed
                {
                    Title = "Execution succeeded!",
                    Description = string.Join("\n", obj.Stdout.Select(x => x.Text)),
                    Color = Color.Green,
                };

                string stdout = string.Join("\n", obj.Stdout.Select(x => x.Text));
                string stderr = string.Join("\n", obj.Stderr.Select(x => x.Text));

                if (!string.IsNullOrEmpty(stdout))
                    eb.AddField("stdout", stdout);

                if (!string.IsNullOrEmpty(stderr))
                    eb.AddField("stderr", stderr);

                return new Ok(eb);
            }

            [Command("decomp")]
            [Description("Given a compiler and code, this will output generated intel assembly from its decompilation")]
            public async Task<Result> Decompile(string compiler, [Remainder] string code)
            {
                code = Util.Utilities.ExtractCode(code);

                if (!_bolt.Compilers.Contains(compiler))
                {
                    return Err.AsEmbed("Godbolt does not contain this compiler.");
                }

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var reqBody = new
                {
                    source = code,
                    compiler,
                    options = new
                    {
                        filters = new
                        {
                            demangle = true,
                            directives = true,
                            intel = true,
                            labels = true
                        }
                    },
                    files = Array.Empty<string>()
                };

                var req = new HttpRequestMessage
                {
                    RequestUri = new Uri($"https://godbolt.org/api/compiler/{compiler}/compile"),
                    Content = new StringContent(JsonConvert.SerializeObject(reqBody), Encoding.Default, "application/json"),
                    Method = HttpMethod.Post
                };

                req.Headers.Add("Accept", "application/json");

                HttpResponseMessage res = await Context.HttpClient.SendAsync(req, cts.Token);

                string str = await res.Content.ReadAsStringAsync(cts.Token);

                var obj = JsonConvert.DeserializeObject<CompileResponse>(str);

                if (obj is null)
                    return Err.AsEmbed("Got invalid JSON!");

                // Failed compilation.
                if (obj.Code != 0)
                {
                    string output = obj.Stderr.Count != 0 
                        ? string.Join("\n", obj.Stderr.Select(x => x.Text))
                        : string.Join("\n", obj.Stdout.Select(x => x.Text));

                    // Strip out color codes
                    output = Regex.Replace(output, "\u001b\\[.*?m", string.Empty);

                    return Err.AsCodeBlock("<Compilation Failed>\n\n" + output, "c");
                }

                bool checkSrc = obj.Asm.Any(x => x.Source?.MainSource ?? false);

                bool CheckIsMain(Asm line, int ind) {
                    if (line.Source?.MainSource is bool main)
                        return main;

                    // Check the next line to see if it's part of the main source instead
                    // As some lines were omitted without this
                    if (ind + 1 >= obj.Asm.Count)
                        return false;

                    return obj.Asm[ind + 1].Source?.MainSource ?? false;

                }

                string?[] comp = obj.Asm
                    .Where((x, ind) => !checkSrc || CheckIsMain(x, ind))
                    .Select(x => x.Text).ToArray();

                return Ok.AsCodeBlock(string.Join("\n", comp), "x86asm");
            }

            [Command("compilers")]
            public Result ListCompilersForLanguage(string lang)
            {
                if (!_bolt.LanguageCompilers.TryGetValue(lang, out HashSet<string>? compilers))
                    return Err.AsEmbed("Godbolt does not have this language!");

                var eb = new LocalEmbed
                {
                    Title = $"Compilers for {lang}:",
                    Description = string.Join("\n", compilers)
                };

                return new Ok(eb);
            }

            [Command("languages")]
            public Result ListLanguages()
            {
                var eb = new LocalEmbed
                {
                    Title = "Languages:",
                    Description = string.Join("\n", _bolt.LanguageCompilers.Keys.OrderBy(x => x))
                };

                return new Ok(eb);
            }
        }
    }
}
