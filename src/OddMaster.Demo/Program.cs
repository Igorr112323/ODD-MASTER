using System;

namespace OddMaster.Demo
{
    /// <summary>Временная точка входа демо (до M8).</summary>
    public static class Program
    {
        /// <summary>Печатает версию каркаса.</summary>
        public static int Main(string[] args)
        {
            Console.WriteLine("ОДД-Мастер: каркас, этап M0. Команда: " + (args.Length > 0 ? args[0] : "all"));
            return 0;
        }
    }
}
