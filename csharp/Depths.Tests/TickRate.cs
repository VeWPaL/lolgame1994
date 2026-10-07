using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// The suite runs at the JS rate: the parity rows were recorded there and the older C#-only tests were
// written against it. A fixture or test that wants another rate says so with its own [TickRate].
[assembly: Depths.Tests.TickRate(Depths.Balance.JsHz)]

namespace Depths.Tests
{
    /// <summary>
    /// Runs a test (or every test under it) at a tick rate, and restores the rate after, so a rate
    /// set by one test never leaks into the next. Inner attributes run after outer ones, so they win.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TickRateAttribute : Attribute, ITestAction
    {
        readonly int _hz;
        readonly Stack<int> _was = new Stack<int>();   // suite and test calls nest on one instance

        public TickRateAttribute(int hz) { _hz = hz; }

        public ActionTargets Targets => ActionTargets.Suite | ActionTargets.Test;

        public void BeforeTest(ITest test) { _was.Push(Balance.TickHz); Balance.TickHz = _hz; }

        public void AfterTest(ITest test) => Balance.TickHz = _was.Pop();
    }
}
