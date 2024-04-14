//
// Formula One Name Interchanger

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;

namespace foni
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Foni(args[0]).Run(args);
        }
    }
}