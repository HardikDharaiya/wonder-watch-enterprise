using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Services;
using WonderWatch.Domain.Entities;
using WonderWatch.Domain.Enums;
using WonderWatch.Infrastructure;
using Xunit;

namespace WonderWatch.Tests
{
    public class OrderServiceTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
                .Options;

            return new AppDbContext(options);
        }

        private OrderService CreateOrderService(AppDbContext context)
        {
            var mockLogger = new Mock<ILogger<OrderService>>();
            return new OrderService(context, mockLogger.Object);
        }
        [Fact]
        public async Task CreateOrder_ValidData_CreatesOrderAndDeductsStock()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = CreateOrderService(context);
            var userId = Guid.NewGuid();
            var watchId = Guid.NewGuid();

            var watch = new Watch
            {
                Id = watchId,
                Name = "Test Watch",
                RetailPrice = 500000m,
                StockQuantity = 5,
                IsPublished = true
            };
            context.Watches.Add(watch);
            await context.SaveChangesAsync();

            var dto = new CreateOrderDto
            {
                Line1 = "123 Luxury Lane",
                City = "Mumbai",
                State = "Maharashtra",
                PinCode = "400001",
                Phone = "9876543210",
                Items = new List<CartItemDto>
                {
                    new CartItemDto { WatchId = watchId, Quantity = 2 }
                }
            };

            // Act
            var order = await service.CreateOrderAsync(userId, dto);

            // Assert
            Assert.NotNull(order);
            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal(1000000m, order.TotalAmount); // 500,000 * 2
            Assert.Single(order.Items);

            var updatedWatch = await context.Watches.FindAsync(watchId);
            Assert.NotNull(updatedWatch);
            Assert.Equal(3, updatedWatch.StockQuantity); // 5 - 2
            Assert.False(updatedWatch.IsSoldOut);
        }

        [Fact]
        public async Task CreateOrder_InsufficientStock_ThrowsInvalidOperationException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = CreateOrderService(context);
            var userId = Guid.NewGuid();
            var watchId = Guid.NewGuid();

            var watch = new Watch
            {
                Id = watchId,
                Name = "Test Watch",
                RetailPrice = 500000m,
                StockQuantity = 1, // Only 1 in stock
                IsPublished = true
            };
            context.Watches.Add(watch);
            await context.SaveChangesAsync();

            var dto = new CreateOrderDto
            {
                Line1 = "123 Luxury Lane",
                City = "Mumbai",
                State = "Maharashtra",
                PinCode = "400001",
                Phone = "9876543210",
                Items = new List<CartItemDto>
                {
                    new CartItemDto { WatchId = watchId, Quantity = 2 } // Trying to buy 2
                }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOrderAsync(userId, dto));
            Assert.Contains("Insufficient stock", exception.Message);
        }
        [Theory]
        [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
        [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Paid, OrderStatus.Processing)]
        [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
        public async Task TransitionStatus_ValidTransition_UpdatesStatus(OrderStatus currentStatus, OrderStatus newStatus)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = CreateOrderService(context);
            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                UserId = Guid.NewGuid(),
                Status = currentStatus,
                TotalAmount = 100000m,
                ShippingAddress = new Address { Line1 = "Test", City = "Test", State = "Test", PinCode = "123", Phone = "123" }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Act
            await service.TransitionStatusAsync(orderId, newStatus);

            // Assert
            var updatedOrder = await context.Orders.FindAsync(orderId);
            Assert.NotNull(updatedOrder);
            Assert.Equal(newStatus, updatedOrder.Status);
        }

        [Theory]
        [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
        [InlineData(OrderStatus.Processing, OrderStatus.Pending)]
        public async Task TransitionStatus_InvalidTransition_ThrowsInvalidOperationException(OrderStatus currentStatus, OrderStatus newStatus)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = CreateOrderService(context);
            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                UserId = Guid.NewGuid(),
                Status = currentStatus,
                TotalAmount = 100000m,
                ShippingAddress = new Address { Line1 = "Test", City = "Test", State = "Test", PinCode = "123", Phone = "123" }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransitionStatusAsync(orderId, newStatus));
            Assert.Contains("Illegal state transition", exception.Message);
        }

        [Fact]
        public async Task TransitionStatus_ToCancelled_RestoresStock()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = CreateOrderService(context);
            var orderId = Guid.NewGuid();
            var watchId = Guid.NewGuid();

            var watch = new Watch
            {
                Id = watchId,
                Name = "Test Watch",
                RetailPrice = 500000m,
                StockQuantity = 0,
                IsSoldOut = true
            };
            context.Watches.Add(watch);

            var order = new Order
            {
                Id = orderId,
                UserId = Guid.NewGuid(),
                Status = OrderStatus.Pending,
                TotalAmount = 500000m,
                ShippingAddress = new Address { Line1 = "Test", City = "Test", State = "Test", PinCode = "123", Phone = "123" },
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = Guid.NewGuid(), WatchId = watchId, Quantity = 1, UnitPrice = 500000m }
                }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Act
            await service.TransitionStatusAsync(orderId, OrderStatus.Cancelled);

            // Assert
            var updatedWatch = await context.Watches.FindAsync(watchId);
            Assert.NotNull(updatedWatch);
            Assert.Equal(1, updatedWatch.StockQuantity); // Stock restored
            Assert.False(updatedWatch.IsSoldOut); // Sold out flag removed
        }
    }
}