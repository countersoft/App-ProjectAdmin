using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using Countersoft.Foundation.Commons.Extensions;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.Permissions;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Gemini.Infrastructure.Apps;
using Countersoft.Gemini.Models;
using System.Linq;
using System.Text;
using Countersoft.Gemini.Infrastructure.Helpers;
using Countersoft.Foundation.Commons.Enums;
using Countersoft.Gemini.Commons.Entity.Security;
using Countersoft.Gemini.Commons.Meta;
using ProjectAdmin.Models;
using System.Diagnostics;
using Countersoft.Gemini;
using System.IO;
using Countersoft.Gemini.Infrastructure.Managers;
using ProjectAdmin.Data;

namespace ProjectAdmin
{
    internal static class Constants
    {
        public static string AppId = "A39E2E45-1D92-4333-A0A9-C60AF393C52B";
        public static string ControlId = "CBAD1E2E-F747-45A4-9920-94FBE530FC6E";
        public static string ProjectAdminSessionView = "ProjectAdminSessionView";

        public const int AutocompleteCount = 100;
    }

    [AppType(AppTypeEnum.FullPage), 
    AppGuid("A39E2E45-1D92-4333-A0A9-C60AF393C52B"),
    AppControlGuid("CBAD1E2E-F747-45A4-9920-94FBE530FC6E"), 
    AppAuthor("Countersoft"), AppKey("projectAdmin"),
    AppName("Project Admin"),
    AppDescription("Project Admin"),
    AppControlUrl("view")]
    [OutputCache(Duration = 0, NoStore = false, Location = OutputCacheLocation.None)]
    public class PageProjectAdmin : BaseAppController
    {
        public override WidgetResult Show(IssueDto issue = null)
        {
            var filter = IsSessionFilter() || CurrentCard.CardType != ProjectTemplatePageType.Custom && "app/projectAdmin/view".Equals(CurrentCard.Url, StringComparison.InvariantCultureIgnoreCase) ? HttpSessionManager.GetFilter(CurrentCard.Id, IssuesFilter.CreateProjectFilter(CurrentUser.Entity.Id, CurrentProject.Entity.Id)) : CurrentCard.Filter;
            HttpSessionManager.SetFilter(CurrentCard.Id, filter);

            int? currentProjectId = 0;
            
            HttpSessionManager.Set<List<UserIssuesView>>(null, Constants.ProjectAdminSessionView);

            // Safety check required because of http://gemini.countersoft.com/project/DEV/21/item/5088
            PageSettings pageSettings = null;

            try
            {
                if (CurrentCard.Options.ContainsKey(AppGuid))
                {
                    pageSettings = CurrentCard.Options[AppGuid].FromJson<PageSettings>();

                    if (pageSettings.PageData != null)
                    {
                        currentProjectId = pageSettings.PageData.projectId;
                    }
                }
            }
            catch (Exception ex)  {}
       
            //If no project is selected, select the first workspace project.
            if ((!currentProjectId.HasValue || currentProjectId.Value == 0) && filter.Projects.HasValue())
            {
                try
                {                    
                    var workspaceProjects = filter.Projects.Split('|');

                    if (workspaceProjects.Count() > 0)
                    {
                        currentProjectId = Convert.ToInt32(workspaceProjects[0]);
                    }
                }
                catch (Exception ex) { }
            }

            var viewableProjects = ProjectManager.GetAppViewableProjects(this).ToList();

            //If you can't view the selected project, select first from viewable projects
            if (!viewableProjects.Any(s => s.Entity.Id == currentProjectId.Value))
            {
                currentProjectId = viewableProjects.Count > 0 ? viewableProjects.First().Entity.Id : 0;
            }

            UserContext.Project = ProjectManager.Convert(Cache.Projects.Get(currentProjectId.Value));

            ProjectAdminAppModel model = BuildModelData();
            model.ProjectList = new SelectList(viewableProjects, "Entity.Id", "Entity.Name", currentProjectId.GetValueOrDefault());
            
            if (pageSettings == null)
            {
                pageSettings = new PageSettings();
            }

            pageSettings.PageData.projectId = currentProjectId.GetValueOrDefault();

            CurrentCard.Options[AppGuid] = pageSettings.ToJson();
            
            return new WidgetResult() { Success = true, Markup = new WidgetMarkup("views/ProjectAdmin.cshtml", model) };
        }

        public override WidgetResult Caption(IssueDto issue = null)
        {
            return new WidgetResult() { Success = true, Markup = new WidgetMarkup(AppName) };
        }

        [AppUrl("getprojectadmin")]
        public ActionResult GetProjectAdmin(int projectId)
        {
            UserContext.Project = ProjectManager.Convert(Cache.Projects.Get(projectId));

            var model = BuildModelData();

            return JsonSuccess(new
            {
                success = true,
                permission = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_Permissions.cshtml"), model.ProjectAdminAppPermissions),
                groups = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_ProjectGroups.cshtml"), model),
                attributes = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_ProjectAttributes.cshtml"), model)
            });
        }    

        public ProjectAdminAppModel BuildModelData()
        {
            ProjectAdminAppModel model = new ProjectAdminAppModel();
            model.ProjectAdminAppPermissions = new ProjectAdminAppPermissionModel();  

            //Permission Logic
            int id = UserContext.Project.Entity.PermissionId.Value;
            model.ProjectAdminAppPermissions.PermissionSet = new PermissionSetDto(Cache.Permissions.Get(id));
            
            AddRoles(model.ProjectAdminAppPermissions);

            GlobalConfigurationWidgetData<ProjectAdminConfigModel> data = GeminiContext.GlobalConfigurationWidgetStore.Get<ProjectAdminConfigModel>(AppGuid);

            var allProjectGroups = Cache.ProjectGroups.GetAll();

            //Exclude selected groups from admin screen
            List<int> excludeGroups = new List<int>();
            List<int> excludeUsersFromGroups = new List<int>();

            if (data != null && data.Value != null && data.Value.Data != null)
            {
                if (data.Value.Data.ExcludeGroups.Count > 0)
                {
                    excludeGroups = data.Value.Data.ExcludeGroups;
                }

                if (data.Value.Data.ExcludeUserFromGroups.Count > 0)
                {
                    var excludedProjectGroups = allProjectGroups.Where(g => data.Value.Data.ExcludeUserFromGroups.Contains(g.Id));

                    foreach(var group in excludedProjectGroups)
                    {
                        excludeUsersFromGroups.AddRange(group.Members.
                                    Where(a => (a.ProjectId == UserContext.Project.Entity.Id || a.ProjectId == null)
                                          && a.UserId != Countersoft.Gemini.Commons.Constants.AnonymousUserId
                                          && a.UserId != Countersoft.Gemini.Commons.Constants.SystemAccountUserId).Select(s => s.UserId));
                    }
                }
                
            }

            //exclude following groups
            excludeGroups.Add(Countersoft.Gemini.Commons.Constants.GlobalGroupEveryone);
            excludeGroups.Add(Countersoft.Gemini.Commons.Constants.GlobalGroupEveryoneAuthenticated);
            excludeGroups.Add(Countersoft.Gemini.Commons.Constants.GlobalGroupAdministrators);

            model.ProjectGroups = new List<ProjectGroupModel>();
            var projectGroups = allProjectGroups.Where(g => !excludeGroups.Contains(g.Id));
            
            foreach (var projectGroup in projectGroups)
            {
                int myProjects = projectGroup.Members.FindAll(m => m.ProjectId == UserContext.Project.Entity.Id && !excludeUsersFromGroups.Contains(m.UserId)).Count();
                
                ProjectGroupModel projectGroupModel = new ProjectGroupModel();

                projectGroupModel.Summary = GetSummary(myProjects);
                projectGroupModel.ProjectGroup = projectGroup;

                model.ProjectGroups.Add(projectGroupModel);
            }            
            
            model.ProjectAttributes = ProjectAttributeRepository.GetAll(UserContext.Project.Entity.Id);

            return model;
        }

        private void AddRoles(ProjectAdminAppPermissionModel model)
        {
            model.Roles = new List<ProjectAdminAppPermissionsRoleModel>();
            ProjectAdminAppPermissionsRoleModel role = new ProjectAdminAppPermissionsRoleModel();

            List<ProjectGroup> groups = Cache.ProjectGroups.GetAll();

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanViewProject,
                Label = "Can View Project",
            };

            List<int> groupMembers  = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanViewProject).Select(m => m.MemberId).ToList();
            List<ProjectGroup> projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanBeAssigedWork,
                Label = "Can Be Assigned Work (Resource)",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanBeAssigedWork).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanDeleteItem,
                Label = "Can Delete Item",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanDeleteItem).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanMoveCopyItem,
                Label = "Can Move/Copy Item",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanMoveCopyItem).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanPerformBulkOperations,
                Label = "Can Perform Bulk Operations",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanPerformBulkOperations).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);


            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanDeleteComment,
                Label = "Can Delete Comment",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanDeleteComment).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanSequenceItems,
                Label = "Can Sequence Items",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanSequenceItems).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanManageComponents,
                Label = "Can Manage Components",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanManageComponents).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);


            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanManageVersions,
                Label = "Can Manage Versions",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanManageVersions).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanSetProjectDefaultValues,
                Label = "Can Set Project Default Values",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanSetProjectDefaultValues).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanOnlyDeleteOwnComment,
                Label = "Can Only Delete Own Comment",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanOnlyDeleteOwnComment).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);


            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanOnlyViewOwnItems,
                Label = "Can Only View Own Items",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanOnlyViewOwnItems).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanOnlyViewOwnOrganizationItems,
                Label = "Can Only View Own Organization Items",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanOnlyViewOwnOrganizationItems).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanOnlyAmendOwnItems,
                Label = "Can Only Amend Own Items",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanOnlyAmendOwnItems).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);


            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.CanOnlyDeleteOwnItems,
                Label = "Can Only Delete Own Items",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.CanOnlyDeleteOwnItems).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

            role = new ProjectAdminAppPermissionsRoleModel
            {
                Role = Roles.ReadOnly,
                Label = "Read Only Access",
            };
            groupMembers = model.PermissionSet.Entity.GetRoles().Where(m => m.Role == (int)Roles.ReadOnly).Select(m => m.MemberId).ToList();
            projectGroups = groups.Where(s => groupMembers.Contains(s.Id)).ToList();
            role.SelectedGlobalGroups = projectGroups.Count > 0 ? string.Join(", ", projectGroups.Select(s => s.Name)) : "";
            model.Roles.Add(role);

        }

        [AppUrl("saveprojectattribute")]
        public ActionResult SaveProjectAttribute(ProjectAttributeModel viewModel)
        {
            ProjectAttribute projectAttribute = (viewModel.Id > 0) ? ProjectAttributeRepository.Get(viewModel.Id) :  new ProjectAttribute();

            projectAttribute.Attributeid = viewModel.Id;
            projectAttribute.ProjectId = viewModel.ProjectId;
            projectAttribute.Attributename = viewModel.Name;
            projectAttribute.Attributevalue = viewModel.Value == null ? string.Empty : viewModel.Value;

            if (viewModel.Id == 0)
            {
                projectAttribute.Attributeorder = ProjectAttributeRepository.GetNextAttributeOrderNumber(viewModel.ProjectId);
            }

            int id = ProjectAttributeRepository.Create(projectAttribute);
            if (id > 0)
            {
                ProjectAdminAppModel model = new ProjectAdminAppModel();
                model.ProjectAttributes = ProjectAttributeRepository.GetAll(viewModel.ProjectId);

                return JsonSuccess(new
                {
                    Html = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_ProjectAttributes.cshtml"), model)
                });
            }
            else
            {
                return JsonError();
            }
        }

        [AppUrl("resequenceattribute")]
        public ActionResult ResequenceAttribute(int projectId, int id, int afterid, int newIndex, int oldIndex)
        {
            var projectAttributes = ProjectAttributeRepository.GetAll(projectId);
            
            ProjectAttribute entity = projectAttributes.Find(s => s.Attributeid == id);
            projectAttributes.RemoveAll(s => s.Attributeid == id);

            if (newIndex > -1) projectAttributes.Insert(newIndex, entity);

            int order = 1;
            projectAttributes.ForEach(s => s.Attributeorder = order++);
            projectAttributes.ForEach(s => ProjectAttributeRepository.Create(s));

            return JsonSuccess();
        }

        [AppUrl("deleteprojectattribute")]
        public ActionResult DeleteteProjectAttribute(int id)
        {   
            ProjectAttribute attribute = ProjectAttributeRepository.Get(id);

            if (attribute != null)
            {
                ProjectAttributeRepository.Delete(id);

                return JsonSuccess(new { Id = id});
            }

            return JsonError();
        }

        [AppUrl("getprojectattributeeditor")]
        public ActionResult GetProjectAttributeEditor(int projectId, int id = 0)
        {            
            var model = new ProjectAttribute();

            model.ProjectId = projectId;
            model.Attributeid = id;

            if (id > 0)
            {
                var projectAttribute = ProjectAttributeRepository.Get(id);
                if (projectAttribute != null)
                {
                    model = projectAttribute;
                }
            }

            return JsonSuccess(new
            {
                Html = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_ProjectAttributeEditor.cshtml"), model)
            });
        }

        private List<int> GetExcludedUsers(int projectId)
        {
            GlobalConfigurationWidgetData<ProjectAdminConfigModel> data = GeminiContext.GlobalConfigurationWidgetStore.Get<ProjectAdminConfigModel>(AppGuid);

            List<int> excludeUserFromGroups = new List<int>();

            if (data != null && data.Value != null && data.Value.Data != null && data.Value.Data.ExcludeUserFromGroups.Count > 0)
            {
                var excludedProjectGroups = Cache.ProjectGroups.GetAll().Where(g => data.Value.Data.ExcludeUserFromGroups.Contains(g.Id));

                foreach (var group in excludedProjectGroups)
                {
                    excludeUserFromGroups.AddRange(group.Members.
                        Where(a => (a.ProjectId == projectId || a.ProjectId == null)
                            && a.UserId != Countersoft.Gemini.Commons.Constants.AnonymousUserId
                            && a.UserId != Countersoft.Gemini.Commons.Constants.SystemAccountUserId).Select(s => s.UserId));
                }
            }

            return excludeUserFromGroups;
        }

        [AppUrl("projectgroupeditor")]
        public ActionResult ProjectGroupsEditor(int id, int projectId)
        {
            List<int> excludeUserFromGroups = GetExcludedUsers(projectId);

            ProjectAdminProjectGroupMembersModel model = new ProjectAdminProjectGroupMembersModel();

            model.Group = new ProjectGroup(Cache.ProjectGroups.Get(id));
            var members = new List<ProjectGroupMembership>(model.Group.Members);
            members.RemoveAll(m => m.UserId == Countersoft.Gemini.Commons.Constants.SystemAccountUserId);
            model.Group.Members = members;

            model.Projects = new List<ProjectAdminProjectGroupMembersModel.ProjectAdminProjectGroupMembershipModel>();
                        
            ProjectDto project = new ProjectDto(Cache.Projects.Get(projectId));

            ProjectAdminProjectGroupMembersModel.ProjectAdminProjectGroupMembershipModel p =
                new ProjectAdminProjectGroupMembersModel.ProjectAdminProjectGroupMembershipModel();

            p.Id = project.Entity.Id;
            p.Name = project.Entity.Name;
            int? newProjectId = p.Id == Countersoft.Gemini.Commons.Constants.AllProjectsId ? null : new int?(p.Id);
            int myProjects = model.Group.Members.FindAll(m => m.ProjectId == newProjectId && !excludeUserFromGroups.Contains(m.UserId)).Count();
            
            p.Summary = GetSummary(myProjects);

            model.Projects.Add(p);

            //Get list of users
            List<User> users = Cache.Users.GetAll().FindAll(u => u.Active && u.Id != Countersoft.Gemini.Commons.Constants.SystemAccountUserId);
            users.Sort((x, y) => x.Fullname.ToLowerInvariant().CompareTo(y.Fullname.ToLowerInvariant()));

            List<User> myMembers;
            if (projectId == Countersoft.Gemini.Commons.Constants.AllProjectsId)
            {
                myMembers = users.FindAll(u => model.Group.MembersForProject(null).ToList().Find(m => m.UserId == u.Id) != null);
            }
            else
            {
                List<ProjectGroupMembership> allMembers = new List<ProjectGroupMembership>(model.Group.MembersForProject(null));
                myMembers = users.FindAll(u => model.Group.MembersForProject(projectId).ToList().Find(m => m.UserId == u.Id) != null);

                users.RemoveAll(u => allMembers.Find(m => m.UserId == u.Id) != null || excludeUserFromGroups.Contains(u.Id));                
            }

            var selectedUserIds = myMembers.Select(u => u.Id).ToList();

            List<User> selectedUsers = new List<User>();
            List<User> remainingUsers = new List<User>();

            foreach (var user in users)
            {
                if (selectedUserIds.Contains(user.Id))
                {
                    selectedUsers.Add(user);
                }
                else if (remainingUsers.Count < Constants.AutocompleteCount)
                {
                    remainingUsers.Add(user);
                }
            }

            if (remainingUsers.Count > 0) selectedUsers.AddRange(remainingUsers);

            model.Users = new MultiSelectList(selectedUsers, "Id", "Fullname", selectedUserIds);
            model.Classes = users.Count > Constants.AutocompleteCount ? "no-chosen auto-complete-chosen" : string.Empty;

            return JsonSuccess(new
            {
                success = true,                
                Html = RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_ProjectGroupMembers.cshtml"), model)
            });
        }

        public string GetSummary(int myProjects)
        {
            return string.Format("{0} {1}", myProjects, myProjects > 1 ? ResourceKeys.Users : ResourceKeys.User);      
        }

        [AppUrl("saveprojectgroupmembers")]
        public ActionResult SaveProjectGroupMembers()
        {
            ProjectGroupManager projectGroupManager = new ProjectGroupManager(Cache, UserContext, GeminiContext);

            int groupId = Request["PGPICKER-GROUP"].ToInt();

            int? projectId = Request["PGPICKER-PROJECT"].ToInt();

            if (projectId == 0) projectId = null;

            var group = Cache.ProjectGroups.Get(groupId);

            var members = Request["PGPICKER__" + groupId].SplitEntries(',', 0);

            var newMembers = members.Except(group.MembersForProject(projectId).Select(gg => gg.UserId));

            var removedMembers = group.MembersForProject(projectId).Select(gg => gg.UserId).Except(members);

            foreach (var newMember in newMembers)
            {                
                projectGroupManager.CreateMembership(new ProjectGroupMembership() { ProjectGroupId = group.Id, UserId = newMember, ProjectId = projectId });
            }

            var excludedUsers = GetExcludedUsers(projectId.GetValueOrDefault());
            foreach (var removedMember in removedMembers)
            {
                // Check if the removed member is not part of the exclude settings and then remove!
                if (!excludedUsers.Contains(removedMember))
                {
                    projectGroupManager.DeleteMembership(group.Id, removedMember, projectId);
                }
            }

            if (groupId == Countersoft.Gemini.Commons.Constants.GlobalGroupAdministrators)
            {
                projectGroupManager.CreateMembership(new ProjectGroupMembership() { ProjectGroupId = Countersoft.Gemini.Commons.Constants.GlobalGroupAdministrators, UserId = Countersoft.Gemini.Commons.Constants.SystemAccountUserId, ProjectId = null });
            }

            group = GeminiContext.ProjectGroups.Get(groupId);
            var groupMembers = new List<ProjectGroupMembership>(group.Members);
            groupMembers.RemoveAll(m => m.UserId == Countersoft.Gemini.Commons.Constants.SystemAccountUserId);
            group.Members = groupMembers;

            int myProjects = group.Members.FindAll(m => m.ProjectId == projectId.Value && !excludedUsers.Contains(m.UserId)).Count();

            return JsonSuccess(new
            {
                Html = GetSummary(myProjects)
            });
             
        }

        [AppUrl("getcustomfieldvalue")]
        public ActionResult GetCustomFieldValue(string term, string cf)
        {
            var cfId = IssueFieldsHelper.GetChosenCustomFieldDetails(cf);
            List<ListItem> data = new List<ListItem>();
            if (cfId == 0)
            {
                if (cf.StartsWith("pgpicker", StringComparison.InvariantCultureIgnoreCase))
                {                   
                    int groupId = Convert.ToInt32(cf.Substring(cf.IndexOf("__") + 2, cf.Length - "PGPICKER___chosen".Length));

                    ProjectGroup group = Cache.ProjectGroups.Get(groupId);

                    List<ProjectGroupMembership> allMembers = new List<ProjectGroupMembership>(group.MembersForProject(null));

                    var users = UserManager.Convert(Cache.Users.FindAll(u => u.Active
                                                                    && u.Id != Countersoft.Gemini.Commons.Constants.SystemAccountUserId 
                                                                    && u.Fullname.Contains(term, StringComparison.InvariantCultureIgnoreCase)
                                                                    && !allMembers.Any(s => s.UserId == u.Id))
                                                                    .Take(100).ToList());
                    data.AddRange(UserManager.ToListItem(users, new List<int>()));
                }

                return JsonSuccess(data);
            }

            data.AddRange(CustomFieldManager.GetCustomFieldLookUp(cfId, CurrentProject.Entity.Id == 0 ? null : new int?(CurrentProject.Entity.Id), term, 100, CurrentIssue));

            return JsonSuccess(data);
        }

        public override WidgetResult Configuration()
        {
            var result = new WidgetResult() { Success = true };

            ProjectAdminConfigModel model = GetConfigModel();

            result.Markup = new WidgetMarkup("views/settings.cshtml", model);

            return result;
        }

        public ProjectAdminConfigModel GetConfigModel()
        {
            ProjectAdminConfigModel model = new ProjectAdminConfigModel();

            GlobalConfigurationWidgetData<ProjectAdminConfigModel> data = GeminiContext.GlobalConfigurationWidgetStore.Get<ProjectAdminConfigModel>(AppGuid);

            if (data != null && data.Value != null && data.Value.Data != null)
            {
                var item = data.Value.Data;

                if (item != null)
                {
                    model.Data = item;
                }
            }
            var projectGroups = Cache.ProjectGroups.GetAll();

            var groups = projectGroups.Where(g => g.Id != Countersoft.Gemini.Commons.Constants.GlobalGroupEveryone
                && g.Id != Countersoft.Gemini.Commons.Constants.GlobalGroupEveryoneAuthenticated
                && g.Id != Countersoft.Gemini.Commons.Constants.GlobalGroupAdministrators);

            var excludeUserGroups = projectGroups.Where(g => g.Id != Countersoft.Gemini.Commons.Constants.GlobalGroupEveryone
                && g.Id != Countersoft.Gemini.Commons.Constants.GlobalGroupEveryoneAuthenticated);

            model.Groups = new MultiSelectList(groups, "Id", "Name", model.Data.ExcludeGroups);
            model.UserGroups = new MultiSelectList(excludeUserGroups, "Id", "Name", model.Data.ExcludeUserFromGroups);

            return model;
        }

        [AppUrl("saveconfigpage")]
        public ActionResult SaveConfigPage()
        {
            var groupIds = Request["groupIds[]"].SplitEntries(',', 0);
            var userGroupIds = Request["userGroupIds[]"].SplitEntries(',', 0);

            GlobalConfigurationWidgetData<ProjectAdminConfigModel> data = GeminiContext.GlobalConfigurationWidgetStore.Get<ProjectAdminConfigModel>(AppGuid);

            if (data == null)
            {
                data = new GlobalConfigurationWidgetData<ProjectAdminConfigModel>();
                data.AppId = AppGuid;
                data.Value = new ProjectAdminConfigModel();
                data.Value.Data = new ProjectAdminConfigData();
            }

            data.Value.Data.ExcludeGroups = groupIds;
            data.Value.Data.ExcludeUserFromGroups = userGroupIds;

            GeminiContext.GlobalConfigurationWidgetStore.Save(AppGuid, data.Value);

            return JsonSuccess();
        }
    }
}
