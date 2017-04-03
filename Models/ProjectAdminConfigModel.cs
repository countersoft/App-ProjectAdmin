using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace ProjectAdmin.Models
{
    public class ProjectAdminConfigModel
    {
        public ProjectAdminConfigModel()
        {
            Data = new ProjectAdminConfigData();
        }

        public MultiSelectList Groups { get; set; }
        public MultiSelectList UserGroups { get; set; }
        public ProjectAdminConfigData Data { get; set; }
    }

    public class ProjectAdminConfigData
    {
        public ProjectAdminConfigData()
        {
            ExcludeGroups = new List<int>();
        }

        public List<int> ExcludeGroups { get; set; }
        public List<int> ExcludeUserFromGroups { get; set; }
    }
}
