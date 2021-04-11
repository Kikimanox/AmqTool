using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AmqTool
{
    public class MyLocker : DispatcherObject
    {
        public bool IsLocked { get; private set; }

        public void PerformLockAction(Action action)
        {
            if (action == null)
                return;

            IsLocked = true;
            Dispatcher.Invoke(action);
            IsLocked = false;
        }
    }
}
