#if MULTIPLAYER_TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Multiplayer.MetricTypes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    public class MessagingMetricsTests : DualClientMetricTestBase
    {
        const uint MessageNameHashSize = 5;
        const uint MessageContentStringLength = 1;

        const uint MessageOverhead = MessageNameHashSize + MessageContentStringLength;

        protected override int NbClients => 2;

        [UnityTest]
        public IEnumerator TrackNamedMessageSentMetric()
        {
            var messageName = Guid.NewGuid().ToString();
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(messageName);

            var waitForMetricValues = new WaitForMetricValues<NamedMessageEvent>(ServerMetrics.Dispatcher, MetricNames.NamedMessageSent);

            Server.CustomMessagingManager.SendNamedMessage(messageName, FirstClient.LocalClientId, memoryStream);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var namedMessageSentMetricValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, namedMessageSentMetricValues.Count);

            var namedMessageSent = namedMessageSentMetricValues.First();
            Assert.AreEqual(messageName, namedMessageSent.Name);
            Assert.AreEqual(FirstClient.LocalClientId, namedMessageSent.Connection.Id);
            Assert.AreEqual(messageName.Length + MessageOverhead, namedMessageSent.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackNamedMessageSentMetricToMultipleClients()
        {
            var messageName = Guid.NewGuid().ToString();
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(messageName);

            var waitForMetricValues = new WaitForMetricValues<NamedMessageEvent>(ServerMetrics.Dispatcher, MetricNames.NamedMessageSent);
            Server.CustomMessagingManager.SendNamedMessage(messageName, new List<ulong> { FirstClient.LocalClientId, SecondClient.LocalClientId }, memoryStream);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var namedMessageSentMetricValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(2, namedMessageSentMetricValues.Count);
            Assert.That(namedMessageSentMetricValues.Select(x => x.Name), Has.All.EqualTo(messageName));
            Assert.That(namedMessageSentMetricValues.Select(x => x.BytesCount), Has.All.EqualTo(messageName.Length + MessageOverhead));
        }

        [UnityTest]
        public IEnumerator TrackNamedMessageReceivedMetric()
        {
            var messageName = Guid.NewGuid().ToString();
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(messageName);

            LogAssert.Expect(LogType.Log, $"Received from {Server.LocalClientId}");
            FirstClient.CustomMessagingManager.RegisterNamedMessageHandler(messageName, (sender, payload) =>
            {
                Debug.Log($"Received from {sender}");
            });

            var waitForMetricValues = new WaitForMetricValues<NamedMessageEvent>(FirstClientMetrics.Dispatcher, MetricNames.NamedMessageReceived);

            Server.CustomMessagingManager.SendNamedMessage(messageName, FirstClient.LocalClientId, memoryStream);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var namedMessageReceivedValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, namedMessageReceivedValues.Count);

            var namedMessageReceived = namedMessageReceivedValues.First();
            Assert.AreEqual(messageName, namedMessageReceived.Name);
            Assert.AreEqual(Server.LocalClientId, namedMessageReceived.Connection.Id);
            Assert.AreEqual(messageName.Length + MessageOverhead, namedMessageReceived.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackUnnamedMessageSentMetric()
        {
            var message = Guid.NewGuid().ToString();
            using var buffer = new NetworkBuffer();
            buffer.Write(Encoding.UTF8.GetBytes(message));

            var waitForMetricValues = new WaitForMetricValues<UnnamedMessageEvent>(ServerMetrics.Dispatcher, MetricNames.UnnamedMessageSent);
            Server.CustomMessagingManager.SendUnnamedMessage(FirstClient.LocalClientId, buffer);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var unnamedMessageSentMetricValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, unnamedMessageSentMetricValues.Count);

            var unnamedMessageSent = unnamedMessageSentMetricValues.First();
            Assert.AreEqual(FirstClient.LocalClientId, unnamedMessageSent.Connection.Id);
            Assert.AreEqual(message.Length, unnamedMessageSent.BytesCount);
        }

        [UnityTest]
        public IEnumerator TrackUnnamedMessageSentMetricToMultipleClients()
        {
            var message = Guid.NewGuid().ToString();
            using var buffer = new NetworkBuffer();
            buffer.Write(Encoding.UTF8.GetBytes(message));

            var waitForMetricValues = new WaitForMetricValues<UnnamedMessageEvent>(ServerMetrics.Dispatcher, MetricNames.UnnamedMessageSent);
            Server.CustomMessagingManager.SendUnnamedMessage(new List<ulong> { FirstClient.LocalClientId, SecondClient.LocalClientId }, buffer);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var unnamedMessageSentMetricValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(2, unnamedMessageSentMetricValues.Count);
            Assert.That(unnamedMessageSentMetricValues.Select(x => x.BytesCount), Has.All.EqualTo(message.Length));

            var clientIds = unnamedMessageSentMetricValues.Select(x => x.Connection.Id).ToList();
            Assert.Contains(FirstClient.LocalClientId, clientIds);
            Assert.Contains(SecondClient.LocalClientId, clientIds);
        }

        [UnityTest]
        public IEnumerator TrackUnnamedMessageReceivedMetric()
        {
            var message = Guid.NewGuid().ToString();
            using var buffer = new NetworkBuffer();
            buffer.Write(Encoding.UTF8.GetBytes(message));

            var waitForMetricValues = new WaitForMetricValues<UnnamedMessageEvent>(FirstClientMetrics.Dispatcher, MetricNames.UnnamedMessageReceived);

            Server.CustomMessagingManager.SendUnnamedMessage(FirstClient.LocalClientId, buffer);

            yield return waitForMetricValues.WaitForMetricsReceived();

            var unnamedMessageReceivedValues = waitForMetricValues.AssertMetricValuesHaveBeenFound();
            Assert.AreEqual(1, unnamedMessageReceivedValues.Count);

            var unnamedMessageReceived = unnamedMessageReceivedValues.First();
            Assert.AreEqual(Server.LocalClientId, unnamedMessageReceived.Connection.Id);
            Assert.AreEqual(message.Length, unnamedMessageReceived.BytesCount);
        }
    }
}
#endif