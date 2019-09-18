using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using TechTalk.SpecFlow;
using TestBenchmark.Lib;

namespace TestBenchmark.Specs
{
    [Binding]
    public class CountingSteps
    {
        private Counter counter;
        private int lastReturnFromIncrement;

        [Given("I have created a Counter")]
        public void GivenIHaveCreatedACounter()
        {
            this.counter = new Counter();
        }

        [Given("I have called Increment")]
        [When("I call Increment again")]
        public void GivenIHaveCalledIncrement()
        {
            this.lastReturnFromIncrement = this.counter.Increment();
        }

        [Then("the last value returned from Increment should be (.*)")]
        public void ThenTheLastValueReturnedFromIncrementShouldBe(int expected)
        {
            Assert.AreEqual(expected, this.lastReturnFromIncrement);
        }
    }
}
