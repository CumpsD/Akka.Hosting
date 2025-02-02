﻿//-----------------------------------------------------------------------
// <copyright file="AllTestForEventFilterBase.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Event;
using Akka.TestKit;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using static FluentAssertions.FluentActions;

namespace Akka.Hosting.TestKit.Tests.TestEventListenerTests
{
    public abstract class AllTestForEventFilterBase<TLogEvent> : EventFilterTestBase where TLogEvent : LogEvent
    {
        // ReSharper disable ConvertToLambdaExpression
        private EventFilterFactory? _testingEventFilter;

        protected AllTestForEventFilterBase(LogLevel logLevel, ITestOutputHelper? output = null)
            : base(logLevel, output)
        {
        }

        protected override async Task BeforeTestStart()
        {
            await base.BeforeTestStart();
            LogLevel = Event.Logging.LogLevelFor<TLogEvent>();
            // ReSharper disable once VirtualMemberCallInConstructor
            _testingEventFilter = CreateTestingEventFilter();
        }

        protected new LogLevel LogLevel { get; private set; }
        protected abstract EventFilterFactory CreateTestingEventFilter();

        protected void LogMessage(string message)
        {
            Log.Log(LogLevel, message);
        }

        protected override void SendRawLogEventMessage(object message)
        {
            PublishMessage(message, "test");
        }

        protected abstract void PublishMessage(object message, string source);

        [Fact]
        public void Single_message_is_intercepted()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel).ExpectOne(() => LogMessage("whatever"));
            TestSuccessful = true;
        }


        [Fact]
        public void Can_intercept_messages_when_start_is_specified()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, start: "what").ExpectOne(() => LogMessage("whatever"));
            TestSuccessful = true;
        }

        [Fact]
        public void Do_not_intercept_messages_when_start_does_not_match()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, start: "what").ExpectOne(() =>
            {
                LogMessage("let-me-thru");
                LogMessage("whatever");
            });
            ExpectMsg<TLogEvent>(err => (string)err.Message == "let-me-thru");
            TestSuccessful = true;
        }

        [Fact]
        public void Can_intercept_messages_when_message_is_specified()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, message: "whatever").ExpectOne(() => LogMessage("whatever"));
            TestSuccessful = true;
        }

        [Fact]
        public void Do_not_intercept_messages_when_message_does_not_match()
        {
            EventFilter.ForLogLevel(LogLevel, message: "whatever").ExpectOne(() =>
            {
                LogMessage("let-me-thru");
                LogMessage("whatever");
            });
            ExpectMsg<TLogEvent>(err => (string)err.Message == "let-me-thru");
            TestSuccessful = true;
        }

        [Fact]
        public void Can_intercept_messages_when_contains_is_specified()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, contains: "ate").ExpectOne(() => LogMessage("whatever"));
            TestSuccessful = true;
        }

        [Fact]
        public void Do_not_intercept_messages_when_contains_does_not_match()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, contains: "eve").ExpectOne(() =>
            {
                LogMessage("let-me-thru");
                LogMessage("whatever");
            });
            ExpectMsg<TLogEvent>(err => (string)err.Message == "let-me-thru");
            TestSuccessful = true;
        }


        [Fact]
        public void Can_intercept_messages_when_source_is_specified()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, source: LogSource.FromType(GetType(), Sys)).ExpectOne(() => LogMessage("whatever"));
            TestSuccessful = true;
        }

        [Fact]
        public void Do_not_intercept_messages_when_source_does_not_match()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel, source: "expected-source").ExpectOne(() =>
            {
                PublishMessage("message", source: "expected-source");
                PublishMessage("message", source: "let-me-thru");
            });
            ExpectMsg<TLogEvent>(err => err.LogSource == "let-me-thru");
            TestSuccessful = true;
        }

        [Fact]
        public void Specified_numbers_of_messages_and_be_intercepted()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel).Expect(2, () =>
            {
                LogMessage("whatever");
                LogMessage("whatever");
            });
            TestSuccessful = true;
        }

        [Fact]
        public void Expect_0_events_Should_work()
        {
            this.Invoking(_ =>
            {
                EventFilter.Error().Expect(0, () =>
                {
                    Log.Error("something");
                });
            }).Should().Throw<Exception>("Expected 0 events");
        }

        [Fact]
        public async Task ExpectAsync_0_events_Should_work()
        {
            Exception? ex = null;
            try
            {
                await EventFilter.Error().ExpectAsync(0, async () =>
                {
                    await Task.Delay(100); // bug only happens when error is not logged instantly
                    Log.Error("something");
                });
            }
            catch (Exception e)
            {
                ex = e;
            }

            ex.Should().NotBeNull("Expected 0 errors logged, but there are error logs");
        }

        /// issue: InternalExpectAsync does not await actionAsync() - causing actionAsync to run as a detached task #5537
        [Fact]
        public async Task ExpectAsync_should_await_actionAsync()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            await Assert.ThrowsAnyAsync<FalseException>(async () =>
            {
                await _testingEventFilter.ForLogLevel(LogLevel).ExpectAsync(0, actionAsync: async () =>
                {
                    Assert.False(true);
                    await Task.CompletedTask;
                });
            });
        }

        // issue: InterceptAsync seems to run func() as a detached task #5586
        [Fact]
        public async Task InterceptAsync_should_await_func()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            await Assert.ThrowsAnyAsync<FalseException>(async () =>
            {
                await _testingEventFilter.ForLogLevel(LogLevel).ExpectAsync(0, async () =>
                {
                    Assert.False(true);
                    await Task.CompletedTask;
                }, TimeSpan.FromSeconds(.1));
            });
        }

        [Fact]
        public void Messages_can_be_muted()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel).Mute(() =>
            {
                LogMessage("whatever");
                LogMessage("whatever");
            });
            TestSuccessful = true;
        }


        [Fact]
        public void Messages_can_be_muted_from_now_on()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            var unmutableFilter = _testingEventFilter.ForLogLevel(LogLevel).Mute();
            LogMessage("whatever");
            LogMessage("whatever");
            unmutableFilter.Unmute();
            TestSuccessful = true;
        }

        [Fact]
        public void Messages_can_be_muted_from_now_on_with_using()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            using(_testingEventFilter.ForLogLevel(LogLevel).Mute())
            {
                LogMessage("whatever");
                LogMessage("whatever");
            }
            TestSuccessful = true;
        }


        [Fact]
        public void Make_sure_async_works()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter.ForLogLevel(LogLevel).Expect(1, TimeSpan.FromSeconds(2), () =>
            {
                Task.Delay(TimeSpan.FromMilliseconds(10)).ContinueWith(_ => { LogMessage("whatever"); });
            });
        }

        [Fact]
        public void Chain_many_filters()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            _testingEventFilter
                .ForLogLevel(LogLevel,message:"Message 1").And
                .ForLogLevel(LogLevel,message:"Message 3")
                .Expect(2,() =>
                 {
                     LogMessage("Message 1");
                     LogMessage("Message 2");
                     LogMessage("Message 3");

                 });
            ExpectMsg<TLogEvent>(m => (string) m.Message == "Message 2");
        }


        [Fact]
        public void Should_timeout_if_too_few_messages()
        {
            if (_testingEventFilter is null)
                throw new NullReferenceException("_testingEventFilter should not be null, check CreateTestingEventFilter implementation.");
            
            Invoking(() =>
            {
                _testingEventFilter.ForLogLevel(LogLevel).Expect(2, TimeSpan.FromMilliseconds(50), () =>
                {
                    LogMessage("whatever");
                });
            }).Should().Throw<TrueException>().WithMessage("timeout*");
        }

        [Fact]
        public void Should_log_when_not_muting()
        {
            const string message = "This should end up in the log since it's not filtered";
            LogMessage(message);
            ExpectMsg<TLogEvent>( msg => (string)msg.Message == message);
        }

        // ReSharper restore ConvertToLambdaExpression

    }
}

