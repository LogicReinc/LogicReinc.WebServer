using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Enums
{
    public enum IOHandlingType
    {
        Synchronously,
        WorkerPool,
        DefaultThreadpool,
        TaskRun
    }
}
