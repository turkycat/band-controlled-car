using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace band_controlled_car
{
    public abstract class Controller
    {
        public enum Turn
        {
            none,
            left,
            right
        }

        public enum Direction
        {
            none,
            forward,
            reverse
        }

        public Controller()
        {
            
        }

        public abstract Task<bool> Initialize();
    }
}
