using Castle.MicroKernel.Registration;
using Castle.Windsor;
using MachineArea.Pn.Abstractions;
using Microting.eFormApi.BasePn.Abstractions;
using MachineArea.Pn.Installers;
using Rebus.Bus;
using eFormCore;
using Microsoft.EntityFrameworkCore;
using Microting.eFormMachineAreaBase.Infrastructure.Data;

namespace MachineArea.Pn.Services
{
    public class RebusService : IRebusService
    {
        private IBus _bus;
        private IWindsorContainer _container;
        private string _connectionString;
        private readonly IEFormCoreService _coreHelper;

        public RebusService(IEFormCoreService coreHelper)
        {            
            //_dbContext = dbContext;
            _coreHelper = coreHelper;
        }

        public void Start(string connectionString)
        {
            _connectionString = connectionString;   
            _container = new WindsorContainer();
            _container.Install(
                new RebusHandlerInstaller()
                , new RebusInstaller(connectionString, 1, 1)
            );
            
            Core _core = _coreHelper.GetCore();
            _container.Register(Component.For<Core>().Instance(_core));
            _container.Register(Component.For<MachineAreaPnDbContext>().Instance(GetContext()));
            _bus = _container.Resolve<IBus>();
        }

        public IBus GetBus()
        {
            return _bus;
        }
        
        private MachineAreaPnDbContext GetContext()
        {

            DbContextOptionsBuilder dbContextOptionsBuilder = new DbContextOptionsBuilder<MachineAreaPnDbContext>();

            if (_connectionString.ToLower().Contains("convert zero datetime"))
            {
                dbContextOptionsBuilder.UseMySql(_connectionString);
            }
            else
            {
                dbContextOptionsBuilder.UseSqlServer(_connectionString);
            }
            dbContextOptionsBuilder.UseLazyLoadingProxies(true);
            return new MachineAreaPnDbContext(dbContextOptionsBuilder.Options);

        }        
    }
}