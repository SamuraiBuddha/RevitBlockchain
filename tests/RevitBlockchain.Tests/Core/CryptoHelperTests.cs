using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using RevitBlockchain.Core;

namespace RevitBlockchain.Tests.Core
{
    public class CryptoHelperTests
    {
        [Fact]
        public void CalculateSHA256_ShouldReturnConsistentHash()
        {
            // Arrange
            var input = "test data";

            // Act
            var hash1 = CryptoHelper.CalculateSHA256(input);
            var hash2 = CryptoHelper.CalculateSHA256(input);

            // Assert
            hash1.Should().Be(hash2);
            hash1.Should().HaveLength(64); // SHA256 is 64 hex characters
            hash1.Should().MatchRegex("^[a-f0-9]+$"); // Only lowercase hex
        }

        [Fact]
        public void CalculateSHA256_DifferentInputs_ShouldReturnDifferentHashes()
        {
            // Arrange
            var input1 = "test data 1";
            var input2 = "test data 2";

            // Act
            var hash1 = CryptoHelper.CalculateSHA256(input1);
            var hash2 = CryptoHelper.CalculateSHA256(input2);

            // Assert
            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void CalculateMerkleRoot_SingleHash_ShouldReturnSameHash()
        {
            // Arrange
            var hashes = new List<string> { "abcdef123456" };

            // Act
            var merkleRoot = CryptoHelper.CalculateMerkleRoot(hashes);

            // Assert
            merkleRoot.Should().Be(hashes[0]);
        }

        [Fact]
        public void CalculateMerkleRoot_MultipleHashes_ShouldReturnRoot()
        {
            // Arrange
            var hashes = new List<string>
            {
                CryptoHelper.CalculateSHA256("tx1"),
                CryptoHelper.CalculateSHA256("tx2"),
                CryptoHelper.CalculateSHA256("tx3"),
                CryptoHelper.CalculateSHA256("tx4")
            };

            // Act
            var merkleRoot = CryptoHelper.CalculateMerkleRoot(hashes);

            // Assert
            merkleRoot.Should().NotBeNullOrEmpty();
            merkleRoot.Should().HaveLength(64);
            merkleRoot.Should().NotBe(hashes[0]); // Should be different from any input
        }

        [Fact]
        public void GenerateUniqueId_ShouldGenerateUniqueIds()
        {
            // Arrange & Act
            var id1 = CryptoHelper.GenerateUniqueId();
            System.Threading.Thread.Sleep(1); // Ensure different timestamp
            var id2 = CryptoHelper.GenerateUniqueId();

            // Assert
            id1.Should().NotBe(id2);
            id1.Should().MatchRegex(@"^\d+-\d{6}-[a-f0-9]{8}$");
        }

        [Fact]
        public void SignData_ShouldProduceConsistentSignatures()
        {
            // Arrange
            var data = "important data";
            var privateKey = "my-private-key";

            // Act
            var sig1 = CryptoHelper.SignData(data, privateKey);
            var sig2 = CryptoHelper.SignData(data, privateKey);

            // Assert
            sig1.Should().Be(sig2);
        }

        [Fact]
        public void VerifySignature_ValidSignature_ShouldReturnTrue()
        {
            // Arrange
            var data = "important data";
            var key = "my-key";
            var signature = CryptoHelper.SignData(data, key);

            // Act
            var isValid = CryptoHelper.VerifySignature(data, signature, key);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void VerifySignature_InvalidSignature_ShouldReturnFalse()
        {
            // Arrange
            var data = "important data";
            var key = "my-key";
            var signature = "invalid-signature";

            // Act
            var isValid = CryptoHelper.VerifySignature(data, signature, key);

            // Assert
            isValid.Should().BeFalse();
        }
    }
}
