using System;
using Xunit;

namespace Monitor.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var test = new Monitor.Function();

            var result = test.FunctionHandler(new[] { "test", "test" }, null);
        }

        [Fact]
        public void Test2()
        {
            var test = new Monitor.Function();

            var result = test.GetNumberOfLINTError("1234");
        }
    }
}
