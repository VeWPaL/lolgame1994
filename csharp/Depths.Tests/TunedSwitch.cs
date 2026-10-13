using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// The suite plays the JS: the parity rows were recorded before the C# playtest tuning (Balance.Tuned).
// A fixture or test that wants the tuning says [TunedOn(true)].
[assembly: Depths.Tests.TunedOn(false)]

namespace Depths.Tests
{
    /// <summary>Sets <see cref="Balance.Tuned"/> for a test (or everything under it) and restores it after, as [CurveOn] does the curve.</summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TunedOnAttribute : Attribute, ITestAction
    {
        readonly bool _on;
        readonly Stack<bool> _was = new Stack<bool>();   // suite and test calls nest on one instance

        public TunedOnAttribute(bool on) { _on = on; }

        public ActionTargets Targets => ActionTargets.Suite | ActionTargets.Test;

        public void BeforeTest(ITest test) { _was.Push(Balance.Tuned); Balance.Tuned = _on; }

        public void AfterTest(ITest test) => Balance.Tuned = _was.Pop();
    }
}
