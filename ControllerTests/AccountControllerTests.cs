using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CESystem.ClientPart;
using CESystem.Controllers;
using CESystem.DB;
using CESystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CESystem.Tests.ControllerTests
{
    public class AccountControllerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly Mock<LocalDbContext> _dbMock;
        private readonly ControllerContext _controllerContext;
        
        public AccountControllerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _dbMock = new Mock<LocalDbContext>(new DbContextOptions<LocalDbContext>());
            
            var fakeHttpContext = new Mock<HttpContext>(MockBehavior.Strict);
            fakeHttpContext.SetupGet(hc => hc.User.Identity.Name).Returns("test");
            _controllerContext = new ControllerContext
            {
                HttpContext = fakeHttpContext.Object
            };
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public async Task ChooseAccount_CheckAccountBelonging(int accountId)
        {
            //arrange    
            var testUser = TestUser();
            var userAccounts = GetTestUserAccounts();
            var account = null as AccountRecord;
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(testUser);
            userServiceMock
                .Setup(x => x.FindUserAccountAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback((int u, int a) =>
                {
                    account = userAccounts.FirstOrDefault(ar => ar.Id == a && ar.UserId == u);
                }).ReturnsAsync(() => account);
            
            var accountController = new AccountController(userServiceMock.Object, _dbMock.Object)
            {
                ControllerContext = _controllerContext
            };

            //act 
            var result = await accountController.ChooseAccount(accountId);
            
            //assert
            Assert.IsType<NotFoundResult>(result);
        }
        
        [Theory]
        [InlineData(OperationType.Transfer, "testUser2")]
        [InlineData(OperationType.Withdraw, null)]
        public async Task Operation_MakeMoneyReductionOperations_ReturnOperationDenied(OperationType operationType, string toUserName)
        {
            //arrange
            var fakeUser = TestUser();
            var fakeAmount = 100.0f;
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var fakeAccount = TestUserAccount();
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            userServiceMock
                .Setup(x => x.FindUserWalletAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(null as WalletRecord);
            userServiceMock
                .Setup(x => x.FindUserAccountAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeAccount);
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeUser);
            
            var accountController = new AccountController(userServiceMock.Object, _dbMock.Object)
            {
                ControllerContext = _controllerContext
            };
            
            //act
            var result = await accountController.Operation(fakeAccount.Id, operationType, fakeAmount, fakeCurrency.Name, toUserName);
            
            //assert
            Assert.IsType<BadRequestObjectResult>(result);
            userServiceMock.Verify(x => x.FindUserWalletAsync(It.IsAny<int>(), It.IsAny<int>()));
        }

        [Fact]
        public async Task Operation_ConfirmLimitCondition_ReturnsOkWithNewRequests()
        {
            //arrange
            var fakeUser = TestUser();
            var fakeAmount = 1000.0f;
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE", ConfirmLimit = 999.9f};
            var fakeAccount = TestUserAccount();
            var fakeWallet = new WalletRecord { CashValue = 1000};
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            userServiceMock
                .Setup(x => x.FindUserWalletAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeWallet);
            userServiceMock
                .Setup(x => x.FindUserAccountAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeAccount);
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeUser);
            userServiceMock
                .Setup(x => x.FindCurrencyCommissionAsync(It.IsAny<int>()))
                .ReturnsAsync(null as CommissionRecord);
            userServiceMock
                .Setup(x => x.FindUserCommissionAsync(It.IsAny<int>()))
                .ReturnsAsync(null as CommissionRecord);
            
            var accountController = new AccountController(userServiceMock.Object, _dbMock.Object)
            {
                ControllerContext = _controllerContext
            }; 
            
            //act 
            var result = await accountController.Operation(fakeAccount.Id, OperationType.Deposit, fakeAmount, fakeCurrency.Name, null);

            //assert
            Assert.IsType<OkObjectResult>(result);
            userServiceMock.Verify(x => x.AddRequestToConfirmAsync(It.IsAny<OperationType>(),
                It.IsAny<AccountRecord>(),
                It.IsAny<AccountRecord>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<string>()));
        }

        [Fact]
        public async Task Operation_WithdrawMoneyWithCommission_VerifyResult()
        {
             //arrange
            var fakeUser = TestUser();
            var fakeAmount = 1000.0f;
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var fakeAccount = TestUserAccount();
            var fakeWallet = new WalletRecord {CashValue = 1010};
            var fakeCommission = new CommissionRecord {CurrencyId = 1, WithdrawCommission = 10, IsAbsoluteType = true};
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            userServiceMock
                .Setup(x => x.FindUserWalletAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeWallet);
            userServiceMock
                .Setup(x => x.FindUserAccountAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeAccount);
            userServiceMock
                .Setup(x => x.FindUserByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeUser);
            userServiceMock
                .Setup(x => x.FindCurrencyCommissionAsync(It.IsAny<int>()))
                .ReturnsAsync(fakeCommission);
            userServiceMock
                .Setup(x => x.FindUserCommissionAsync(It.IsAny<int>()))
                .ReturnsAsync(null as CommissionRecord);

            var accountController = new AccountController(userServiceMock.Object, _dbMock.Object)
            {
                ControllerContext = _controllerContext
            }; 
            
            //act
            await accountController.Operation(fakeAccount.Id, OperationType.Withdraw, fakeAmount, fakeCurrency.Name, null);
            
            //assert
            Assert.Equal(0, fakeWallet.CashValue);
        }
        private UserRecord TestUser() => new UserRecord
        {
            Id = 1,
            Name = "test",
        };
        
        private AccountRecord TestUserAccount() => new AccountRecord
        {
            Id = 1,
            UserId = 1
        };

        private List<AccountRecord> GetTestUserAccounts()
        {
            return new List<AccountRecord>
            {
                new AccountRecord {Id = 1, UserId = 1},
                new AccountRecord {Id = 2, UserId = 2},
                new AccountRecord {Id = 3, UserId = 3},
                new AccountRecord {Id = 4, UserId = 1},
                new AccountRecord {Id = 5, UserId = 3},
                new AccountRecord {Id = 6, UserId = 4},
            };
        }
    }
}