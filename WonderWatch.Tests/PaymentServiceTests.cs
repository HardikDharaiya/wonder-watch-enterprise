using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WonderWatch.Application.Services;
using Xunit;

namespace WonderWatch.Tests
{
    public class PaymentServiceTests
    {
        private const string TestSecretKey = "wonder_watch_test_secret_8921";

        private PaymentService CreatePaymentService(string? secretKey = TestSecretKey)
        {
            var mockConfig = new Mock<IConfiguration>();

            // Mock the IConfiguration indexer to return our test secret
            mockConfig.Setup(c => c["Razorpay:KeySecret"]).Returns(secretKey);

            var mockLogger = new Mock<ILogger<PaymentService>>();

            return new PaymentService(mockConfig.Object, mockLogger.Object);
        }

        private string GenerateValidSignature(string orderId, string paymentId, string secret)
        {
            var payload = $"{orderId}|{paymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        [Fact]
        public void VerifySignature_ValidSignature_ReturnsTrue()
        {
            // Arrange
            var service = CreatePaymentService();
            var orderId = "order_TEST123";
            var paymentId = "pay_TEST456";

            // Generate a mathematically correct signature using the test secret
            var validSignature = GenerateValidSignature(orderId, paymentId, TestSecretKey);

            // Act
            var result = service.VerifySignature(orderId, paymentId, validSignature);

            // Assert
            Assert.True(result, "A mathematically valid HMAC-SHA256 signature should return true.");
        }

        [Fact]
        public void VerifySignature_InvalidSignature_ReturnsFalse()
        {
            // Arrange
            var service = CreatePaymentService();
            var orderId = "order_TEST123";
            var paymentId = "pay_TEST456";

            // Generate a signature using a DIFFERENT secret (simulating a forged request)
            var forgedSignature = GenerateValidSignature(orderId, paymentId, "wrong_secret_key");

            // Act
            var result = service.VerifySignature(orderId, paymentId, forgedSignature);

            // Assert
            Assert.False(result, "A signature generated with the wrong secret must return false.");
        }

        [Fact]
        public void VerifySignature_TamperedPayload_ReturnsFalse()
        {
            // Arrange
            var service = CreatePaymentService();
            var orderId = "order_TEST123";
            var paymentId = "pay_TEST456";

            // Generate a valid signature for the original payload
            var validSignature = GenerateValidSignature(orderId, paymentId, TestSecretKey);

            // Act
            // Simulate an attacker changing the paymentId but sending the original signature
            var tamperedPaymentId = "pay_HACKER999";
            var result = service.VerifySignature(orderId, tamperedPaymentId, validSignature);

            // Assert
            Assert.False(result, "If the payload is tampered with, the original signature must fail verification.");
        }

        [Fact]
        public void VerifySignature_MissingSecretInConfig_ThrowsInvalidOperationException()
        {
            // Arrange
            // Create service with a null secret key to simulate missing configuration
            var service = CreatePaymentService(null);
            var orderId = "order_TEST123";
            var paymentId = "pay_TEST456";
            var signature = "dummy_signature";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.VerifySignature(orderId, paymentId, signature));

            Assert.Contains("Razorpay KeySecret missing", exception.Message);
        }
    }
}