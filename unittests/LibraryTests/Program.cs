//
// Copyright (c) 2008, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Reflection;
using System.Runtime.Remoting;

namespace LibraryTests
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            AppDomainSetup domSetup = new AppDomainSetup();
            domSetup.ApplicationBase = @"C:\Program Files\NUnit 2.4.8\";// @"C:\";
            domSetup.PrivateBinPath = @"C:\Program Files\NUnit 2.4.8\bin\";

            AppDomain dom = AppDomain.CreateDomain("Test Run", null, domSetup);

            object invoker = dom.CreateInstanceFromAndUnwrap(typeof(Program).Assembly.Location, "LibraryTests.Invoker");
            invoker.GetType().GetMethod("DoUnitTests").Invoke(invoker, new object[] { Assembly.GetExecutingAssembly().Location });

        }
    }

    public class Invoker : MarshalByRefObject
    {
        public void DoUnitTests(string assembly)
        {
            ObjectHandle objHandle = Activator.CreateInstanceFrom(@"C:\Program Files\NUnit 2.4.8\bin\nunit-gui-runner.dll", "NUnit.Gui.AppEntry");
            objHandle.Unwrap().GetType().GetMethod("Main").Invoke(objHandle.Unwrap(), new object[] { new string[] { assembly } });
        }
    }
}
