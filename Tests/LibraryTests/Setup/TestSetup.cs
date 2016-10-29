using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Complete;
using NUnit.Framework;

public static class TestSetup
{
    [OneTimeSetUp]
    public static void RunBeforeAnyTests()
    {
        SetupHelper.SetupComplete();
    }
}
