using CaraNegra.Application.Categorias.Commands;
using CaraNegra.Application.Categorias.DTOs;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CaraNegra.Tests.Commands;

public class CreateCategoriaCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly CreateCategoriaCommandHandler _handler;

    public CreateCategoriaCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _handler = new CreateCategoriaCommandHandler(_contextMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateCategoriaAndReturnDto()
    {
        // Arrange
        var dto = new CreateCategoriaDto
        {
            Nombre = "Bebidas",
            Descripcion = "Todo tipo de bebidas"
        };
        var command = new CreateCategoriaCommand(dto);

        var categoriasDbSet = new List<Categoria>();
        var mockSet = CreateMockDbSet(categoriasDbSet);

        _contextMock.Setup(x => x.Categorias).Returns(mockSet.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback<CancellationToken>(_ => categoriasDbSet.Add(new Categoria 
            { 
                CategoriaId = 1, 
                Nombre = dto.Nombre, 
                Descripcion = dto.Descripcion, 
                EstaActivo = true,
                CreadoEn = DateTime.UtcNow 
            }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Nombre.Should().Be(dto.Nombre);
        result.Descripcion.Should().Be(dto.Descripcion);
        result.EstaActivo.Should().BeTrue();

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        mockSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(data.Add);
        return mockSet;
    }
}