using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeepSeek;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using DeepSeek.Classes;

namespace DeepSeek.Tests
{
    [TestClass]
    public class DeepSeekTests
    {
        private DeepSeekClient? _client;
        private bool _disposed;
        
        private static readonly string? ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
            ?? "DEEPSEEK_API_KEY"; 

        [TestInitialize]
        public void Initialize()
        {
            if (ApiKey == "DEEPSEEK_API_KEY" || string.IsNullOrEmpty(ApiKey))
            {
                Assert.Inconclusive("API key not set");
            }

            _client = new DeepSeekClient(ApiKey);
        }

        [TestMethod]
        public async Task ListModelsAsyncTest_ValidKey_ReturnsModels()
        {
            // Act
            var result = await _client!.ListModelsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Data!.Count > 0);
            Assert.IsNull(_client.ErrorMessage);
        }

        [TestMethod]
        public async Task ChatAsyncTest_ValidRequest_ReturnsResponse()
        {
            // Arrange
            var request = new ChatRequest
            {
                Messages = [Message.NewUserMessage("Hello! Tell me an interesting fact about space.")],
                Model = Models.ModelChat
            };

            // Act
            var result = await _client!.ChatAsync(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result!.Choices!.First().Message!.Content));
        }

        [TestMethod]
        public async Task ChatAsyncTest_ValidReasonerRequest_ReturnsReasonerResponse()
        {
            // Arrange
            var request = new ChatRequest
            {
                Messages = [Message.NewUserMessage("Hello! Tell me an interesting fact about space.")],
                Model = Models.ModelReasoner
            };

            // Act
            var result = await _client!.ChatAsync(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result!.Choices!.First().Message!.ReasoningContent));
        }

        [TestMethod]
        public async Task ChatStreamAsyncTest_ValidRequest_StreamsResponses()
        {
            // Arrange
            var request = new ChatRequest
            {
                Messages = [Message.NewUserMessage("Explain the theory of relativity in 3 sentences.")],
                Model = Models.ModelChat
            };

            // Act
            var responses = new List<string>();

            var choices = await _client!.ChatStreamAsync(request, new CancellationToken());
            if (choices is not null)
            {
                await foreach (var choice in choices)
                {
                    responses.Add(choice.Delta?.Content ?? "");
                }
            }
           
            // Assert
            Assert.IsTrue(responses.Count > 0);
            Assert.IsFalse(string.IsNullOrEmpty(string.Join("", responses)));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void SetTimeoutTest_InvalidValue_ThrowsException()
        {
            // Act
            _client!.SetTimeout(-1);
        }


        [TestMethod]
        public async Task ErrorHandlingTest_InvalidKey_ReturnsError()
        {
            // Arrange
            var invalidClient = new DeepSeekClient("invalid_key");

            // Act
            var result = await invalidClient.ListModelsAsync();

            // Assert
            Assert.IsNull(result);
            Assert.IsNotNull(invalidClient.ErrorMessage);
        }
    }
}