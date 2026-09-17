using System;
using System.Collections.Generic;

namespace ET
{
    public class Options: Singleton<Options>
    {
        public string SceneName { get; set; }

        public string StartConfig { get; set; }

        public int Process { get; set; }
        
        public int Develop { get; set; }

        public int LogLevel { get; set; }
        
        public int Console { get; set; }
    }
}
