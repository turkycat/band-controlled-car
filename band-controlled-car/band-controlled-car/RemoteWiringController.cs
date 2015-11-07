using Microsoft.Maker.RemoteWiring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace band_controlled_car
{
    public abstract class RemoteWiringController : Controller
    {
        public RemoteDevice Device
        {
            get;
            private set;
        }

        public RemoteWiringController( RemoteDevice device ) : base()
        {
            this.Device = device;
        }
    }
}
