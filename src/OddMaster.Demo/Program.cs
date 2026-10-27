using System;
using OddMaster.Core.Config;

namespace OddMaster.Demo
{
    /// <summary>Точка входа демо-CLI: signs | scheme | check | actualize | all.</summary>
    public static class Program
    {
        /// <summary>Запускает команду демо. Возвращает 0 при успехе.</summary>
        public static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0] : "all";
            if (command != "signs" && command != "scheme" && command != "check"
                && command != "actualize" && command != "all")
            {
                Console.Error.WriteLine("Неизвестная команда: " + command +
                    ". Доступно: signs, scheme, check, actualize, all.");
                return 2;
            }
            var root = DemoFiles.FindRoot();
            var config = ConfigLoader.Load(root);
            var demo = new DemoRunner(root, config);
            switch (command)
            {
                case "signs": demo.Signs(); break;
                case "scheme": demo.Scheme(); break;
                case "check": demo.Check(); break;
                case "actualize": demo.Actualize(); break;
                default:
                    demo.Signs();
                    demo.Scheme();
                    demo.Check();
                    demo.Actualize();
                    break;
            }
            Console.WriteLine("ОДД-Мастер: команда «" + command + "» выполнена.");
            return 0;
        }
    }
}
