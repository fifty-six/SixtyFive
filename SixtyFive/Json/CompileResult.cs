using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SixtyFive.Json
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class CompileResponse
    {
        public CompileResponse
        (
            long code,
            bool okToCache,
            List<TextWrapper> stdout,
            List<TextWrapper> stderr,
            List<Asm> asm,
            bool didExecute
        )
        {
            Code = code;
            OkToCache = okToCache;
            Stdout = stdout;
            Stderr = stderr;
            Asm = asm;
            DidExecute = didExecute;
        }

        public long Code { get; set; }

        public bool OkToCache { get; set; }

        public List<TextWrapper> Stdout { get; set; }

        public List<TextWrapper> Stderr { get; set; }

        public List<Asm> Asm { get; set; }

        public bool DidExecute { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Asm
    {
        public string Text;
        public Source? Source;
        
        public Asm(string text, Source? source)
        {
            Text = text;
            Source = source;
        }
    }

    public class Source
    {
        [JsonProperty("file")]
        public string File;
        
        [JsonProperty("line")]
        public int Line;
        
        [JsonProperty("mainsource")]
        public bool MainSource;

        public Source(string file, int line, bool mainSource)
        {
            File = file;
            Line = line;
            MainSource = mainSource;
        }
    }
}