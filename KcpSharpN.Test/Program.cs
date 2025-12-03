namespace KcpSharpN.Test
{
    internal class Program
    {
        public static int Main()
        {
            Test.test(0);    // 默认模式，类似 TCP：正常模式，无快速重传，常规流控
            Test.test(1);    // 普通模式，关闭流控等
            Test.test(2);    // 快速模式，所有开关都打开，且关闭流控
            return 0;
        }
    }
}
