namespace ET
{
    public class Logger: Singleton<Logger>, ISingletonAwake
    {
        private ILog _log;
        private readonly ConsoleLog _fallbackLog = new();

        public ILog Log
        {
            set
            {
                this._log = value;
            }
            get
            {
                return this._log ?? _fallbackLog;
            }
        }

        public void Awake()
        {
        }
    }
}
