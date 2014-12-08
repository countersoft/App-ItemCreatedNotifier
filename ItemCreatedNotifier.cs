using Countersoft.Gemini.Contracts.Business;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Gemini.Extensibility.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Practices.Unity;
using Countersoft.Gemini.Mailer;
using Countersoft.Gemini.Infrastructure.Managers;
using Countersoft.Gemini.Contracts.Caching;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Entity;
using System.Web.Mvc;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Foundation.Commons.Extensions;
using Countersoft.Gemini.Commons.Permissions;
using Countersoft.Gemini.Infrastructure.Apps;
using System.Web.UI;
using Countersoft.Gemini.Commons.Dto;
using System.Web.Routing;
using Countersoft.Gemini;

namespace ItemCreatedNotifier
{
    public class AppConstants
    {
        public const string AppId = "DE5D3643-8433-4977-81FF-3E05A047DF58";
    }

    public class CreateItemNotifierData
    {
        public int? ProjectId { get; set; }
        public int TemplateId { get; set; }
        public List<int> UserIds { get; set; }

        public CreateItemNotifierData()
        {
            UserIds = new List<int>();
        }
    }

    public class IssueAlertModel
    {
        public IEnumerable<SelectListItem> Users { get; set; }
        public IEnumerable<SelectListItem> Projects { get; set; }
        public IEnumerable<SelectListItem> Templates { get; set; }
    }

    [AppType(AppTypeEnum.Config),
    AppGuid("DE5D3643-8433-4977-81FF-3E05A047DF58"),
    AppControlGuid("D5ED7A38-BE89-403E-8118-E5C3CC8C8E71"),
    AppAuthor("Countersoft"),
    AppKey("ItemCreatedAlert"),
    AppName("Item Created Notifier"),
    AppDescription("Item Created Notifier"),
    AppRequiresConfigScreen(true)]
    [ValidateInput(false)]
    [OutputCache(Duration = 0, NoStore = false, Location = OutputCacheLocation.None)]
    public class CreateItemNotifierController : BaseAppController
    {
        public override void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(null, "apps/createitemnotifier/configure", new { controller = "CreateItemNotifier", action = "GetProjectConfig" });
            routes.MapRoute(null, "apps/createitemnotifier/save", new { controller = "CreateItemNotifier", action = "SaveProjectConfig" });
        }

        public override WidgetResult Caption(IssueDto issue)
        {
            WidgetResult result = new WidgetResult();
            
            result.Success = true;

            result.Markup.Html = "Item Created Notifier";

            return result;
        }

        public override WidgetResult Show(IssueDto args)
        {
            var result = new WidgetResult();
            
            result.Success = true;

            result.Markup.Html = "Item Created Notifier";

            return result;
        }

        public override WidgetResult Configuration()
        {
            var result = new WidgetResult();
            
            var data = GeminiContext.GlobalConfigurationWidgetStore.Get<List<CreateItemNotifierData>>(AppConstants.AppId);
            
            var allData = data == null || data.Value == null || data.Value.Count == 0 ? null : data.Value.Find(d => d.ProjectId.GetValueOrDefault() == 0);
                        
            var model = new IssueAlertModel();
            
            var projects = GeminiContext.Projects.GetAll();
            
            projects.Insert(0, new Project() { Id = 0, Name = "All" });
            
            model.Projects = new SelectList(projects, "Id", "Name", 0);
            
            model.Users = new MultiSelectList(GeminiContext.Users.GetActive(), "Id", "Fullname", allData == null ? null : allData.UserIds);
            
            var alerts = GeminiApp.Container.Resolve<IAlertTemplates>();
            
            var templates = alerts.GetAll();
            
            templates = templates.FindAll(t => t.AlertType == AlertTemplateType.Generic);
            
            templates.Insert(0, new AlertTemplate() { AlertType = AlertTemplateType.Generic, Id = 0, Label = "None" });
            
            model.Templates = new SelectList(templates, "Id", "Label", allData == null ? 0 : allData.TemplateId);

            result.Success = true;
            
            result.Markup = new WidgetMarkup("views/settings.cshtml", model);

            return result;
        }

        public ActionResult SaveProjectConfig(int project, int template, List<int> user)
        {
            var data = GeminiContext.GlobalConfigurationWidgetStore.Get<List<CreateItemNotifierData>>(AppConstants.AppId);
            
            List<CreateItemNotifierData> saveData = new List<CreateItemNotifierData>();
            
            if (data != null && data.Value != null && data.Value.Count > 0)
            {
                saveData = data.Value;
            }

            var projectData = saveData.Find(p => p.ProjectId.GetValueOrDefault() == project);
            
            if (projectData == null)
            {
                projectData = new CreateItemNotifierData();
            
                saveData.Add(projectData);
            }

            projectData.ProjectId = project;
            
            projectData.TemplateId = template;
            
            projectData.UserIds = user;

            GeminiContext.GlobalConfigurationWidgetStore.Save<List<CreateItemNotifierData>>(AppConstants.AppId, saveData);

            return JsonSuccess();
        }

        public ActionResult GetProjectConfig(int projectId)
        {
            var data = GeminiContext.GlobalConfigurationWidgetStore.Get<List<CreateItemNotifierData>>(AppConstants.AppId);
            
            var returnData = new { Users = new List<int>(), Template = 0 };
            
            if (data == null || data.Value == null || data.Value.Count == 0) return JsonSuccess(returnData);
            
            var projectData = data.Value.Find(g => g.ProjectId.GetValueOrDefault() == projectId);
            
            if (projectData == null) return JsonSuccess(returnData);

            returnData = new { Users = projectData.UserIds, Template = projectData.TemplateId };
            
            return JsonSuccess(returnData);
        }
    }

    [AppType(AppTypeEnum.Event),
    AppGuid(AppConstants.AppId),
    AppName("Item Created Notifier"),
    AppDescription("Receive an email when an item is created")]
    public class CreateItemNotifier : AbstractIssueListener
    {
        public override void AfterCreateFull(IssueDtoEventArgs args)
        {
            if (!GeminiApp.Config.EmailAlertsEnabled) return;

            var data = args.Context.GlobalConfigurationWidgetStore.Get<List<CreateItemNotifierData>>(AppConstants.AppId);
            
            if (data == null || data.Value == null || data.Value.Count == 0) return;

            var projectData = data.Value.Find(d => d.ProjectId.GetValueOrDefault() == args.Issue.Entity.ProjectId);
            
            if (projectData == null) projectData = data.Value.Find(d => d.ProjectId.GetValueOrDefault() == 0);
            
            if (projectData == null) return;

            var alertService = GeminiApp.Container.Resolve<IAlertTemplates>();
            
            var alerts = alertService.GetAll();

            var template = alerts.Find(t => t.Id == projectData.TemplateId);
            
            if (template == null) return;

            AlertsTemplateHelper helper = new AlertsTemplateHelper(alerts, args.GeminiUrls[0].Key);
            
            using (var cache = GeminiApp.Container.Resolve<ICacheContainer>())
            {
                var userManager = GeminiApp.GetManager<UserManager>(cache.Users.Find(u => u.Active && u.ProjectGroups.Where(p => p.ProjectGroupId == Constants.GlobalGroupAdministrators && p.UserId == u.Id).Any()));
               
                var metaManager = new MetaManager(userManager);
                
                var types = metaManager.TypeGetAll();

                var OrganizationManager = new OrganizationManager(userManager);

                var organizations = OrganizationManager.GetAll();
                
                var permissions = new PermissionSetManager(userManager);
                
                var permissionSets = permissions.GetAll();
                
                PermissionsManager permissionManager = null;
                
                foreach (var user in projectData.UserIds)
                {
                    var userDto = userManager.Get(user);

                    if (userDto != null && userDto.Entity.Active)
                    {
                        if (permissionManager == null)
                        {
                            permissionManager = new PermissionsManager(userDto, types, permissionSets, organizations, userManager.UserContext.Config.HelpDeskModeGroup, false);
                        }
                        else
                        {
                            permissionManager = permissionManager.Copy(userDto);
                        }

                        if (!permissionManager.CanSeeItem(args.Issue.Project, args.Issue) || !userDto.Entity.EmailMe) continue;
                        
                        var body = helper.Build(template, args.Issue, userDto);
                        
                        string emailLog;
                        
                        EmailHelper.Send(GeminiApp.Config, body.Key.HasValue() ? body.Key : string.Format("[{0}] {1}", args.Issue.IssueKey, args.Issue.Title), body.Value, userDto.Entity.Email, userDto.Entity.Fullname, true, out emailLog);
                    }
                }
            }
        }
    }
}
