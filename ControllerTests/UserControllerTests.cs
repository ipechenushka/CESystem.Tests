using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Helpers;
using Castle.Core.Logging;
using CESystem.ClientPart;
using CESystem.Controllers;
using CESystem.DB;
using CESystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CESystem.Tests.ControllerTests
{
    public class UserControllerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly Mock<ILogger<Startup>> _loggerMock;
        private readonly Mock<LocalDbContext> _dbMock;
        private readonly Mock<IUserService> _userServiceMock;
       
        
        public UserControllerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _loggerMock = new Mock<ILogger<Startup>>();
            _dbMock = new Mock<LocalDbContext>(new DbContextOptions<LocalDbContext>());
            _userServiceMock = new Mock<IUserService>();
        }

        [Theory]
        [InlineData("test1", null)]
        [InlineData(null, "test1")]
        [InlineData(null, null)]
        public async Task Login_CheckRequestParams_ReturnsBadRequest(string name, string password)
        {
            //arrange
            var userController = new UserController(_loggerMock.Object, _userServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await userController.Login(name, password);
            
            //assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
        
        [Fact]
        public async Task Login_FindUser_ReturnsNotFound()
        {
            //arrange
            var userName = "FakeUser";
            var password = "FakePassword";
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(null as UserRecord);
            
            var userController = new UserController(_loggerMock.Object, userServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await userController.Login(userName, password);
            
            //assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
        
        [Theory]
        [InlineData("AFX/qFouDbklkLzF/uu4rhJomRNofJ+/gIFOIAkhAt8WaViE0pgodJRg1uBmBHhcwA==")]
        [InlineData("testSalt")]
        [InlineData("test1")]
        [InlineData("admin")]
        public async Task Login_VerifyUserPassword_ReturnsForbid(string password)
        {
            //arrange
            var userName = "FakeUser";
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(TestUser());
            var userController = new UserController(_loggerMock.Object, userServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await userController.Login(userName, password);

            //assert
            Assert.IsType<ForbidResult>(result);
        }
        
        [Fact]
        public async Task Registration_VerifyAddMethods_ThrowsExceptionOnAuth()
        {
            //arrange
            var userName = "FakeUser";
            var password = "password";
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.AddUserAsync(It.IsAny<UserRecord>()))
                .ReturnsAsync(It.IsAny<EntityEntry<UserRecord>>());
            userServiceMock
                .Setup(x => x.AddAccountAsync(It.IsAny<AccountRecord>()))
                .ReturnsAsync(It.IsAny<EntityEntry<AccountRecord>>());
            var userController = new UserController(_loggerMock.Object, userServiceMock.Object, _dbMock.Object);
            
            //act
            try
            {
                await userController.Registration(userName, password);
            }
            catch (Exception e)
            {
                //assert
                userServiceMock.Verify(x => x.AddUserAsync(It.IsAny<UserRecord>()));
                userServiceMock.Verify(x => x.AddAccountAsync(It.IsAny<AccountRecord>()));
                _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
            }
        }
        
        private UserRecord TestUser() => new UserRecord
        {
            Name = "test",
            PasswordSalt = "DdbAhbs0V9Ug0xEK/r3vFQ==",
            PasswordHash = "AFX/qFouDbklkLzF/uu4rhJomRNofJ+/gIFOIAkhAt8WaViE0pgodJRg1uBmBHhcwA=="
        };
    }
}