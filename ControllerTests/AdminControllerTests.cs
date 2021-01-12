using System.Threading;
using System.Threading.Tasks;
using CESystem.AdminPart;
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
    public class AdminControllerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly Mock<LocalDbContext> _dbMock;
        private readonly Mock<IAdminService> _adminServiceMock;
        
        public AdminControllerTests(ITestOutputHelper testOutputHelper)
        {
            _adminServiceMock = new Mock<IAdminService>();
            _testOutputHelper = testOutputHelper;
            _dbMock = new Mock<LocalDbContext>(new DbContextOptions<LocalDbContext>());
        }

        [Theory]
        [InlineData(MoneyManipulationOperation.Charge, 50, 150)]
        [InlineData(MoneyManipulationOperation.Withdraw, 55, 45)]
        [InlineData(MoneyManipulationOperation.Withdraw, 101, -1)] //should be failed
        public async Task MoneyOperation_MakeWithdrawAndCharge_CheckResult(MoneyManipulationOperation mmo, float fakeAmount, float expectedResult)
        {
            //arrange
            var fakeAccountId = 1;
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var fakeWallet = new WalletRecord {CashValue = 100};
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            userServiceMock
                .Setup(x => x.FindUserWalletAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(fakeWallet);
            var adminController = new AdminController(userServiceMock.Object, _adminServiceMock.Object, _dbMock.Object);
            
            //act 
            var result = await adminController.MoneyOperation(mmo, fakeCurrency.Name, fakeAccountId, fakeAmount);
            
            //assert
            if (expectedResult == -1)
                Assert.IsType<BadRequestObjectResult>(result);
            else
                Assert.Equal(expectedResult, fakeWallet.CashValue);
        }

        [Theory]
        [InlineData(1, 4, 7, true)]
        [InlineData(1, null, 7, null)]
        [InlineData(1, null, null, null)]
        [InlineData(null, 4, 7, true)]
        [InlineData(null, null, null, null)]
        public async Task CommissionOperation_SetPersonalCommissions_CheckCommissions(float? tCom, float? dCom,
            float? wCom, bool? isAbsoluteType)
        {
            var fakeUser = new UserRecord();
            var fakeCommission = new CommissionRecord();
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindUserByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(fakeUser);
            userServiceMock
                .Setup(x => x.FindUserCommissionAsync(It.IsAny<int>()))
                .ReturnsAsync(fakeCommission);
            var adminController = new AdminController(userServiceMock.Object, _adminServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await adminController.CommissionOperation(tCom, dCom, wCom, isAbsoluteType, null, It.IsAny<int>());

            //assert 
            Assert.Equal(tCom, fakeCommission.TransferCommission);
            Assert.Equal(wCom, fakeCommission.WithdrawCommission);
            Assert.Equal(dCom, fakeCommission.DepositCommission);
            Assert.Equal(isAbsoluteType, fakeCommission.IsAbsoluteType);
            Assert.IsType<OkObjectResult>(result);
            _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
        }
        
        [Theory]
        [InlineData(10, 10, 50)]
        [InlineData(60, 1 , 100)] //should be failed
        [InlineData(1, null, null)]
        [InlineData(null, null, null)]
        public async Task CurrencyLimitOperation_SetLimits_CheckLimits(float? lowerLim, float? upperLim,
            float? confirmLim)
        {
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            var adminController = new AdminController(userServiceMock.Object, _adminServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await adminController.CurrencyLimitOperation(fakeCurrency.Name, lowerLim, upperLim, confirmLim);

            //assert 
            Assert.Equal(lowerLim, fakeCurrency.LowerCommissionLimit);
            Assert.Equal(upperLim, fakeCurrency.UpperCommissionLimit);
            Assert.Equal(confirmLim, fakeCurrency.ConfirmLimit);
            Assert.IsType<OkObjectResult>(result);
            _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
        }
        
        [Theory]
        [InlineData(CurrencyManipulationOperation.Add)]
        [InlineData(CurrencyManipulationOperation.Delete)]
        public async Task CurrencyOperation_AddOrDeleteCurrency_VerifyDeleteOrReturnBadRequestForAdding(CurrencyManipulationOperation cmo)
        {
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(It.IsAny<string>()))
                .ReturnsAsync(fakeCurrency);
            var adminController = new AdminController(userServiceMock.Object, _adminServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await adminController.CurrencyOperation(cmo, fakeCurrency.Name);

            //assert 
            if (cmo == CurrencyManipulationOperation.Add)
                Assert.IsType<BadRequestObjectResult>(result);
            else
            {
                Assert.IsType<OkObjectResult>(result);
                _adminServiceMock.Verify(x => x.DeleteCurrency(It.IsAny<CurrencyRecord>()));
                _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
            }
        }

        [Fact]
        public async Task Confirm_VerifyConfirmationAndAddingToOperationsHistory_ReturnOk()
        {
            var fakeCurrency = new CurrencyRecord {Id = 1, Name = "FAKE"};
            var fakeWallet = new WalletRecord { CashValue = 50};
            var fakeUser = new UserRecord { Id = 1};
            var fakeConfirmRequest = new ConfirmRequestRecord
            {
                Currency = "FAKE",
                OperationType = OperationType.Deposit,
                SenderId = 1,
                Amount = 45,
                Commission = 0.0f
            }; 
            var adminServiceMock = new Mock<IAdminService>();
            adminServiceMock
                .Setup(x => x.FindRequestToConfirm(It.IsAny<int>()))
                .ReturnsAsync(fakeConfirmRequest);
            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(x => x.FindCurrencyAsync(fakeConfirmRequest.Currency))
                .ReturnsAsync(fakeCurrency);
            userServiceMock
                .Setup(x => x.FindUserByAccountIdAsync(fakeConfirmRequest.SenderId))
                .ReturnsAsync(fakeUser);
            userServiceMock
                .Setup(x => x.FindUserWalletAsync(fakeConfirmRequest.SenderId, fakeCurrency.Id))
                .ReturnsAsync(fakeWallet);
            var adminController = new AdminController(userServiceMock.Object, adminServiceMock.Object, _dbMock.Object);
            
            //act
            var result = await adminController.Confirm(It.IsAny<int>());

            //assert 
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(RequestStatus.Completed, fakeConfirmRequest.Status);
            userServiceMock.Verify(x => x.AddOperationHistoryAsync(fakeConfirmRequest.OperationType, fakeUser.Id, fakeConfirmRequest.SenderId,
                fakeConfirmRequest.Amount, fakeConfirmRequest.Commission, fakeCurrency.Name));
        }

    }
}