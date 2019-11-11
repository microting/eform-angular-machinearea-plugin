﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Extensions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormOuterInnerResourceBase.Infrastructure.Data;
using Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities;
using OuterInnerResource.Pn.Abstractions;
using OuterInnerResource.Pn.Infrastructure.Models.InnerResources;
using OuterInnerResource.Pn.Messages;
using Rebus.Bus;

namespace OuterInnerResource.Pn.Services
{
    public class InnerResourceService : IInnerResourceService
    {
        private readonly OuterInnerResourcePnDbContext _dbContext;
        private readonly IOuterInnerResourceLocalizationService _localizationService;
        private readonly ILogger<InnerResourceService> _logger;
        private readonly IEFormCoreService _coreService;
        private readonly IBus _bus;

        public InnerResourceService(OuterInnerResourcePnDbContext dbContext,
            IOuterInnerResourceLocalizationService localizationService,
            ILogger<InnerResourceService> logger, 
            IEFormCoreService coreService, 
            IRebusService rebusService)
        {
            _dbContext = dbContext;
            _localizationService = localizationService;
            _logger = logger;
            _coreService = coreService;
            _bus = rebusService.GetBus();
        }

        public async Task<OperationDataResult<InnerResourcesModel>> GetAllMachines(InnerResourceRequestModel requestModel)
        {
            try
            {
                InnerResourcesModel innerResourcesModel = new InnerResourcesModel();

                IQueryable<InnerResource> machinesQuery = _dbContext.InnerResources.AsQueryable();
                if (!string.IsNullOrEmpty(requestModel.Sort))
                {
                    if (requestModel.IsSortDsc)
                    {
                        machinesQuery = machinesQuery
                            .CustomOrderByDescending(requestModel.Sort);
                    }
                    else
                    {
                        machinesQuery = machinesQuery
                            .CustomOrderBy(requestModel.Sort);
                    }
                }
                else
                {
                    machinesQuery = _dbContext.InnerResources
                        .OrderBy(x => x.Id);
                }

                machinesQuery = machinesQuery.Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

                if (requestModel.PageSize != null)
                {
                    machinesQuery = machinesQuery
                        .Skip(requestModel.Offset)
                        .Take((int)requestModel.PageSize);
                }

                List<InnerResourceModel> machines = await machinesQuery.Select(x => new InnerResourceModel()
                {
                    Name = x.Name,
                    Id = x.Id,
                    ExternalId = x.ExternalId,
                    RelatedOuterResourcesIds = _dbContext.OuterInnerResources.Where(y => 
                        y.InnerResourceId == x.Id).Select(z => z.OuterResourceId).ToList()
                }).ToListAsync();

                innerResourcesModel.Total = await _dbContext.InnerResources.CountAsync();
                innerResourcesModel.InnerResourceList = machines;
                
                try
                {
                    innerResourcesModel.Name = _dbContext.PluginConfigurationValues.SingleOrDefault(x => 
                        x.Name == "OuterInnerResourceSettings:InnerResourceName")
                        ?.Value;  
                } catch {}

                return new OperationDataResult<InnerResourcesModel>(true, innerResourcesModel);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                _logger.LogError(e.Message);
                return new OperationDataResult<InnerResourcesModel>(false,
                    _localizationService.GetString("ErrorObtainMachines"));
            }
        }

        public async Task<OperationDataResult<InnerResourceModel>> GetSingleMachine(int machineId)
        {
            try
            {
                InnerResourceModel innerResource = await _dbContext.InnerResources.Select(x => new InnerResourceModel()
                    {
                        Name = x.Name,
                        Id = x.Id,
                        ExternalId = x.ExternalId
                    })
                    .FirstOrDefaultAsync(x => x.Id == machineId);
                                
                if (innerResource == null)
                {
                    return new OperationDataResult<InnerResourceModel>(false,
                        _localizationService.GetString("MachineWithIdNotExist"));
                }
                
                List<Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource> machineAreas = _dbContext.OuterInnerResources
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed && x.InnerResourceId == innerResource.Id).ToList();

                innerResource.RelatedOuterResourcesIds = new List<int>();
                foreach (Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource machineArea in machineAreas)
                {
                    innerResource.RelatedOuterResourcesIds.Add(machineArea.OuterResourceId);
                }
                
                return new OperationDataResult<InnerResourceModel>(true, innerResource);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                _logger.LogError(e.Message);
                return new OperationDataResult<InnerResourceModel>(false,
                    _localizationService.GetString("ErrorObtainMachine"));
            }
        }

        public async Task<OperationResult> CreateMachine(InnerResourceModel model)
        {
            try
            {
                List<Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource> machineAreas 
                    = new List<Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource>();
                if (model.RelatedOuterResourcesIds != null)
                {
                    machineAreas = model.RelatedOuterResourcesIds.Select(x =>
                        new Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource
                        {
                            Id = x
                        }).ToList();    
                }

                InnerResource newArea = new InnerResource()
                {
                    Name = model.Name,
                    ExternalId = model.ExternalId,
                    OuterInnerResources = machineAreas
                };

                await newArea.Create(_dbContext);
                model.Id = newArea.Id;
                await _bus.SendLocal(new OuterInnerResourceCreate(model, null));

                return new OperationResult(true, 
                    _localizationService.GetString("MachineCreatedSuccesfully", model.Name));
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                _logger.LogError(e.Message);
                return new OperationResult(false,
                    _localizationService.GetString("ErrorCreatingMachine"));
            }
        }

        public async Task<OperationResult> UpdateMachine(InnerResourceModel model)
        {
            try
            {
                List<Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource> machineAreas = model.RelatedOuterResourcesIds.Select(x =>
                    new Microting.eFormOuterInnerResourceBase.Infrastructure.Data.Entities.OuterInnerResource
                    {
                        Id = x
                    }).ToList();

                InnerResource selectedMachine = new InnerResource()
                {
                    Name = model.Name,
                    ExternalId = model.ExternalId,
                    OuterInnerResources = machineAreas,
                    Id = model.Id
                };

                await selectedMachine.Update(_dbContext);
                await _bus.SendLocal(new OuterInnerResourceUpdate(model, null));
                return new OperationResult(true, _localizationService.GetString("MachineUpdatedSuccessfully"));
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                _logger.LogError(e.Message);
                return new OperationResult(false,
                    _localizationService.GetString("ErrorUpdatingMachine"));
            }
        }

        public async Task<OperationResult> DeleteMachine(int machineId)
        {
            try
            {
                InnerResource selectedMachine = new InnerResource()
                {
                    Id = machineId
                };
                
                await selectedMachine.Delete(_dbContext);
                await _bus.SendLocal(new OuterInnerResourceDelete(new InnerResourceModel() { Id = machineId }, null));
                return new OperationResult(true, _localizationService.GetString("MachineDeletedSuccessfully"));
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                _logger.LogError(e.Message);
                return new OperationResult(false,
                    _localizationService.GetString("ErrorWhileDeletingMachine"));
            }
        }
    }
}
