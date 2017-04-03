using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons;
using System.Web.Mvc;
using Countersoft.Gemini.Commons.Permissions;
using Countersoft.Gemini.Commons.Entity.Security;

namespace ProjectAdmin.Models
{
    public class ProjectAdminAppModel
    {
        public List<ProjectAttribute> ProjectAttributes;
        public IEnumerable<SelectListItem> ProjectList { get; set; }
        public ProjectAdminAppPermissionModel ProjectAdminAppPermissions { get; set; }

        //public List<ProjectGroup> ProjectGroups { get; set; }
        public List<ProjectGroupModel> ProjectGroups { get; set; }
    }
    
    public class ProjectGroupModel
    {
        public ProjectGroup ProjectGroup { get; set; }
        public string Summary { get; set; }
    }

    public class ProjectAdminAppPermissionModel
    {
        public PermissionSetDto PermissionSet { get; set; }

        //public String Projects { get; set; }
        public MultiSelectList GlobalGroups { get; set; }      

        public List<ProjectAdminAppPermissionsRoleModel> Roles { get; set; }
    }

    public class ProjectAdminAppPermissionsRoleModel
    {
        public string Label { get; set; }
        public Roles Role { get; set; }
        public String SelectedGlobalGroups { get; set; }
        public MultiSelectList SelectedProjectGroups { get; set; }
    }

    public class PageSettings : IssuesGridFilter
    {
        public PageData PageData { get; set; }

        public PageSettings()
        {
            PageData = new PageData();
        }
    }

    public class PageData
    {
        public int projectId { get; set; }
    }

    public class ProjectAdminProjectGroupMembersModel
    {
        public ProjectGroup Group { get; set; }
        public MultiSelectList Users { get; set; }
        public string Classes { get; set; }
        public List<ProjectAdminProjectGroupMembershipModel> Projects { get; set; }

        public class ProjectAdminProjectGroupMembershipModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Summary { get; set; }
        }
    }

    public class ProjectAttributeModel
    {
        public ProjectAttributeModel()
        {
            Value = string.Empty;
        }

        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }      
    }

}