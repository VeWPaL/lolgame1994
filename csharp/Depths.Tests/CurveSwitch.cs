using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// The suite plays the JS ladder: the parity rows were recorded without the C# difficulty curve.
// A fixture or test that wants the curve says [CurveOn(true)].
[assembly: Depths.Tests.CurveOn(false)]

namespace Depths.Tests
{
    /// <summary>Sets <see cref="Curve.On"/> for a test (or everything under it) and restores it after, as [TickRate] does the rate.</summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CurveOnAttribute : Attribute, ITestAction
    {
        readonly bool _on;
        readonly Stack<bool> _was = new Stack<bool>();   // suite and test calls nest on one instance

        public CurveOnAttribute(bool on) { _on = on; }

        public ActionTargets Targets => ActionTargets.Suite | ActionTargets.Test;

        public void BeforeTest(ITest test) { _was.Push(Curve.On); Curve.On = _on; }

        public void AfterTest(ITest test) => Curve.On = _was.Pop();
    }
}
