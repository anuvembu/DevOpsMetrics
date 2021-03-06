﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DevOpsMetrics.Service.Models.AzureDevOps;
using DevOpsMetrics.Service.Models.Common;
using DevOpsMetrics.Service.Models.GitHub;
using DevOpsMetrics.Web.Models;
using DevOpsMetrics.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;

namespace DevOpsMetrics.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration Configuration;

        public HomeController(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            //Get a list of settings
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Return the resultant list
            (List<AzureDevOpsSettings>, List<GitHubSettings>) result = (azureDevOpsSettings, githubSettings);
            return View(result);
        }

        [HttpPost]
        public IActionResult ProjectUpdate(string RowKey, int NumberOfDaysSelected = 30)
        {
            return RedirectToAction("Project", "Home", new { rowKey = RowKey, numberOfDays = NumberOfDaysSelected });
        }

        [HttpGet]
        public async Task<IActionResult> Project(string rowKey, int numberOfDays = 30)
        {
            int maxNumberOfItems = 20;
            bool getSampleData = false;
            bool useCache = true;
            string patToken = Configuration["AppSettings:AzureDevOpsPatToken"];
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];
            ProjectViewModel model = new ProjectViewModel();

            //Find the right project to load
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create the days to view dropdown
            List<NumberOfDaysItem> numberOfDaysList = new List<NumberOfDaysItem>
            {
                new NumberOfDaysItem { NumberOfDays = 7 },
                new NumberOfDaysItem { NumberOfDays = 14 },
                new NumberOfDaysItem { NumberOfDays = 21 },
                new NumberOfDaysItem { NumberOfDays = 30 },
                new NumberOfDaysItem { NumberOfDays = 60 },
                new NumberOfDaysItem { NumberOfDays = 90 }
            };

            //Get Azure DevOps project details
            AzureDevOpsSettings azureDevOpsSetting;
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                if (item.RowKey == rowKey)
                {
                    azureDevOpsSetting = item;

                    DeploymentFrequencyModel deploymentFrequencyModel = await serviceApiClient.GetAzureDevOpsDeploymentFrequency(getSampleData, patToken,
                        item.Organization, item.Project, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                    LeadTimeForChangesModel leadTimeForChangesModel = await serviceApiClient.GetAzureDevOpsLeadTimeForChanges(getSampleData, patToken,
                        item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                    MeanTimeToRestoreModel meanTimeToRestoreModel = await serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                       DevOpsPlatform.AzureDevOps, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                    ChangeFailureRateModel changeFailureRateModel = await serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.Organization, item.Project, item.Branch, item.BuildName,
                        numberOfDays, maxNumberOfItems);
                    deploymentFrequencyModel.IsProjectView = true;
                    leadTimeForChangesModel.IsProjectView = true;
                    meanTimeToRestoreModel.IsProjectView = true;
                    changeFailureRateModel.IsProjectView = true;
                    model = new ProjectViewModel
                    {
                        RowKey = item.RowKey,
                        ProjectName = item.Project,
                        TargetDevOpsPlatform = DevOpsPlatform.AzureDevOps,
                        DeploymentFrequency = deploymentFrequencyModel,
                        LeadTimeForChanges = leadTimeForChangesModel,
                        MeanTimeToRestore = meanTimeToRestoreModel,
                        ChangeFailureRate = changeFailureRateModel,
                        NumberOfDays = new SelectList(numberOfDaysList, "NumberOfDays", "NumberOfDays"),
                        NumberOfDaysSelected = numberOfDays
                    };
                }
            }
            //Get GitHub project details
            GitHubSettings githubSetting;
            foreach (GitHubSettings item in githubSettings)
            {
                if (item.RowKey == rowKey)
                {
                    githubSetting = item;

                    DeploymentFrequencyModel deploymentFrequencyModel = await serviceApiClient.GetGitHubDeploymentFrequency(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                    LeadTimeForChangesModel leadTimeForChangesModel = await serviceApiClient.GetGitHubLeadTimeForChanges(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                    MeanTimeToRestoreModel meanTimeToRestoreModel = await serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.GitHub, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                    ChangeFailureRateModel changeFailureRateModel = await serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.GitHub, item.Owner, item.Repo, item.Branch, item.WorkflowName,
                        numberOfDays, maxNumberOfItems);
                    deploymentFrequencyModel.IsProjectView = true;
                    leadTimeForChangesModel.IsProjectView = true;
                    meanTimeToRestoreModel.IsProjectView = true;
                    changeFailureRateModel.IsProjectView = true;
                    model = new ProjectViewModel
                    {
                        RowKey = item.RowKey,
                        ProjectName = item.Repo,
                        TargetDevOpsPlatform = DevOpsPlatform.GitHub,
                        DeploymentFrequency = deploymentFrequencyModel,
                        LeadTimeForChanges = leadTimeForChangesModel,
                        MeanTimeToRestore = meanTimeToRestoreModel,
                        ChangeFailureRate = changeFailureRateModel,
                        NumberOfDays = new SelectList(numberOfDaysList, "NumberOfDays", "NumberOfDays"),
                        NumberOfDaysSelected = numberOfDays
                    };
                }
            }

            return View(model);
        }

        public async Task<IActionResult> DeploymentFrequency()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 60; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            bool useCache = true; //Use Azure storage instead of hitting the API. Quicker, but data may be up to 4 hours out of date
            string patToken = Configuration["AppSettings:AzureDevOpsPatToken"];
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];

            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<DeploymentFrequencyModel> items = new List<DeploymentFrequencyModel>();

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create deployment frequency models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                DeploymentFrequencyModel newDeploymentFrequencyModel = await serviceApiClient.GetAzureDevOpsDeploymentFrequency(getSampleData, patToken,
                        item.Organization, item.Project, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                newDeploymentFrequencyModel.ItemOrder = item.ItemOrder;
                if (newDeploymentFrequencyModel != null)
                {
                    items.Add(newDeploymentFrequencyModel);
                }
            }
            //Create deployment frequency models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                DeploymentFrequencyModel newDeploymentFrequencyModel = await serviceApiClient.GetGitHubDeploymentFrequency(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                newDeploymentFrequencyModel.ItemOrder = item.ItemOrder;
                if (newDeploymentFrequencyModel != null)
                {
                    items.Add(newDeploymentFrequencyModel);
                }
            }

            //Create the days to view dropdown
            List<NumberOfDaysItem> numberOfDaysList = new List<NumberOfDaysItem>
            {
                new NumberOfDaysItem { NumberOfDays = 7 },
                new NumberOfDaysItem { NumberOfDays = 14 },
                new NumberOfDaysItem { NumberOfDays = 21 },
                new NumberOfDaysItem { NumberOfDays = 30 },
                new NumberOfDaysItem { NumberOfDays = 60 },
                new NumberOfDaysItem { NumberOfDays = 90 }
            };

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            //return View(new ProjectViewModel
            //{
            //    DeploymentFrequency = items[0]
            //}
            //);
            return View(items);
        }

        public async Task<IActionResult> LeadTimeForChanges()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            bool useCache = true; //Use Azure storage instead of hitting the API. Quicker, but data may be up to 4 hours out of date
            string patToken = Configuration["AppSettings:AzureDevOpsPatToken"];
            string clientId = Configuration["AppSettings:GitHubClientId"];
            string clientSecret = Configuration["AppSettings:GitHubClientSecret"];
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<LeadTimeForChangesModel> items = new List<LeadTimeForChangesModel>();

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create lead time for changes models from each Azure DevOps setting object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                LeadTimeForChangesModel newLeadTimeForChangesModel = await serviceApiClient.GetAzureDevOpsLeadTimeForChanges(getSampleData, patToken,
                        item.Organization, item.Project, item.Repository, item.Branch, item.BuildName, item.BuildId,
                        numberOfDays, maxNumberOfItems, useCache);
                newLeadTimeForChangesModel.ItemOrder = item.ItemOrder;
                if (newLeadTimeForChangesModel != null)
                {
                    items.Add(newLeadTimeForChangesModel);
                }
            }
            //Create lead time for changes models from each GitHub setting object
            foreach (GitHubSettings item in githubSettings)
            {
                LeadTimeForChangesModel newLeadTimeForChangesModel = await serviceApiClient.GetGitHubLeadTimeForChanges(getSampleData, clientId, clientSecret,
                        item.Owner, item.Repo, item.Branch, item.WorkflowName, item.WorkflowId,
                        numberOfDays, maxNumberOfItems, useCache);
                newLeadTimeForChangesModel.ItemOrder = item.ItemOrder;
                if (newLeadTimeForChangesModel != null)
                {
                    items.Add(newLeadTimeForChangesModel);
                }
            }

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public async Task<IActionResult> MeanTimeToRestore()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<MeanTimeToRestoreModel> items = new List<MeanTimeToRestoreModel>();

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create MTTR models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                MeanTimeToRestoreModel newMeanTimeToRestoreModel = await serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                newMeanTimeToRestoreModel.ItemOrder = item.ItemOrder;
                if (newMeanTimeToRestoreModel != null)
                {
                    items.Add(newMeanTimeToRestoreModel);
                }
            }
            //Create MTTR models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                MeanTimeToRestoreModel newMeanTimeToRestoreModel = await serviceApiClient.GetAzureMeanTimeToRestore(getSampleData,
                        DevOpsPlatform.GitHub, item.ProductionResourceGroup, numberOfDays, maxNumberOfItems);
                newMeanTimeToRestoreModel.ItemOrder = item.ItemOrder;
                if (newMeanTimeToRestoreModel != null)
                {
                    items.Add(newMeanTimeToRestoreModel);
                }
            }

            //sort the final list
            items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public async Task<IActionResult> ChangeFailureRate()
        {
            int maxNumberOfItems = 20; //20 is the optimium max that looks good with the current UI            
            int numberOfDays = 30; //TODO: Move number of days variable to a drop down list on the current UI 
            bool getSampleData = false;
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);
            List<ChangeFailureRateModel> items = new List<ChangeFailureRateModel>();

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create change failure rate models from each Azure DevOps settings object
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                ChangeFailureRateModel changeFailureRateModel = await serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.AzureDevOps, item.Organization, item.Project, item.Branch, item.BuildName,
                        numberOfDays, maxNumberOfItems);
                //changeFailureRateModel.ItemOrder = item.ItemOrder;
                if (changeFailureRateModel != null)
                {
                    items.Add(changeFailureRateModel);
                }
            }
            //Create change failure rate models from each GitHub settings object
            foreach (GitHubSettings item in githubSettings)
            {
                ChangeFailureRateModel changeFailureRateModel = await serviceApiClient.GetChangeFailureRate(getSampleData,
                        DevOpsPlatform.GitHub, item.Owner, item.Repo, item.Branch, item.WorkflowName,
                        numberOfDays, maxNumberOfItems);
                //changeFailureRateModel.ItemOrder = item.ItemOrder;
                if (changeFailureRateModel != null)
                {
                    items.Add(changeFailureRateModel);
                }
            }

            //sort the final list
            //items = items.OrderBy(o => o.ItemOrder).ToList();
            return View(items);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> ChangeFailureRateUpdates()
        {
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create project items from each Azure DevOps setting and add it to a project list.
            List<ProjectUpdateItem> projectList = new List<ProjectUpdateItem>();
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                ProjectUpdateItem newItem = new ProjectUpdateItem
                {
                    ProjectId = item.RowKey,
                    ProjectName = item.Project
                };
                projectList.Add(newItem);
            }
            //Create project items from each GitHub setting and add it to a project list.
            foreach (GitHubSettings item in githubSettings)
            {
                ProjectUpdateItem newItem = new ProjectUpdateItem
                {
                    ProjectId = item.RowKey,
                    ProjectName = item.Repo
                };
                projectList.Add(newItem);
            }

            //Create a percentage completed dropdown
            List<CompletionPercentItem> completionList = new List<CompletionPercentItem>
            {
                new CompletionPercentItem { CompletionPercent = 0 },
                new CompletionPercentItem { CompletionPercent = 10 },
                new CompletionPercentItem { CompletionPercent = 25 },
                new CompletionPercentItem { CompletionPercent = 50 },
                new CompletionPercentItem { CompletionPercent = 75 },
                new CompletionPercentItem { CompletionPercent = 98 },
                new CompletionPercentItem { CompletionPercent = 100 }
            };

            //Create the days to process dropdown
            List<NumberOfDaysItem> numberOfDaysList = new List<NumberOfDaysItem>
            {
                new NumberOfDaysItem { NumberOfDays = 1 },
                new NumberOfDaysItem { NumberOfDays = 7 },
                new NumberOfDaysItem { NumberOfDays = 21 },
                new NumberOfDaysItem { NumberOfDays = 30 },
                new NumberOfDaysItem { NumberOfDays = 60 },
                new NumberOfDaysItem { NumberOfDays = 90 }
            };

            ProjectUpdateViewModel model = new ProjectUpdateViewModel
            {
                ProjectList = new SelectList(projectList, "ProjectId", "ProjectName"),
                CompletionPercentList = new SelectList(completionList, "CompletionPercent", "CompletionPercent"),
                NumberOfDaysList = new SelectList(numberOfDaysList, "NumberOfDays", "NumberOfDays")
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateChangeFailureRate(string ProjectIdSelected, int CompletionPercentSelected, int NumberOfDaysSelected)
        {
            ServiceApiClient serviceApiClient = new ServiceApiClient(Configuration);

            //Get a list of settings
            List<AzureDevOpsSettings> azureDevOpsSettings = await serviceApiClient.GetAzureDevOpsSettings();
            List<GitHubSettings> githubSettings = await serviceApiClient.GetGitHubSettings();

            //Create project items from each setting and add it to a project list.
            DevOpsPlatform targetDevOpsPlatform = DevOpsPlatform.Unknown;
            string organization_owner = "";
            string project_repo = "";
            string repository = "";
            string buildName_workflowName = "";
            //Update each Azure DevOps setting
            foreach (AzureDevOpsSettings item in azureDevOpsSettings)
            {
                if (item.RowKey == ProjectIdSelected)
                {
                    targetDevOpsPlatform = DevOpsPlatform.AzureDevOps;
                    organization_owner = item.Organization;
                    project_repo = item.Project;
                    repository = item.Repository;
                    buildName_workflowName = item.BuildName;
                }
            }
            //Update each GitHub setting
            foreach (GitHubSettings item in githubSettings)
            {
                if (item.RowKey == ProjectIdSelected)
                {
                    targetDevOpsPlatform = DevOpsPlatform.GitHub;
                    organization_owner = item.Owner;
                    project_repo = item.Repo;
                    repository = "";
                    buildName_workflowName = item.WorkflowName;
                }
            }

            //Update the change failure rate with the % distribution
            if (organization_owner != "" && project_repo != "" && buildName_workflowName != "")
            {
                await serviceApiClient.UpdateChangeFailureRate(organization_owner, project_repo, buildName_workflowName, CompletionPercentSelected, NumberOfDaysSelected);
            }

            //Redirect to the correct project page to see the changes
            if (targetDevOpsPlatform == DevOpsPlatform.AzureDevOps)
            {
                return RedirectToAction("Project", "Home", new { rowKey = organization_owner + "_" + project_repo + "_" + repository + "_" + buildName_workflowName });
            }
            else if (targetDevOpsPlatform == DevOpsPlatform.GitHub)
            {
                return RedirectToAction("Project", "Home", new { rowKey = organization_owner + "_" + project_repo + "_" + buildName_workflowName });
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public IActionResult Generate500Error()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
