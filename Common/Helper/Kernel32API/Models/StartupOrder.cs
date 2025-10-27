using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdIOReader.Models
{
    public enum StartupOrder
    {
        ServerFirst,  // Server khởi động trước, sau đó Client
        ClientFirst   // Client khởi động trước, sau đó Server
    }
}
