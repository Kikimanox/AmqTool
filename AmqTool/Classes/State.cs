using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmqTool
{
    public enum States
    {
        Home, Volume, Clips, Process
    };

    public class State
    {
        public State()
        {
            CurrentState = States.Home;
        }
        public States CurrentState { get; set; } = States.Home;

    }
}
