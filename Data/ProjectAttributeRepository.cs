using Countersoft.Gemini.Extensibility;
using ProjectAdmin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectAdmin.Data
{
    public class ProjectAttributeRepository
    {
        public static int Create(ProjectAttribute projectAttribute)
        {
            var query = "";

            if (projectAttribute.Attributeid > 0)
            {
                query = string.Format(@"UPDATE gemini_projectattributes set 
                                               projectid = @projectid, 
                                               attributename = @attributename, 
                                               attributevalue = @attributevalue, 
                                               attributeorder = @attributeorder where attributeid = @attributeid", 
                                               projectAttribute.ProjectId, 
                                               projectAttribute.Attributename, 
                                               projectAttribute.Attributevalue, 
                                               projectAttribute.Attributeorder,
                                               projectAttribute.Attributeid);
            }
            else
            {
                query = string.Format(@"INSERT INTO gemini_projectattributes (projectid, attributename, attributevalue, attributeorder) 
                values (@projectid, @attributename, @attributevalue, @attributeorder) ", projectAttribute.ProjectId, projectAttribute.Attributename, projectAttribute.Attributevalue, projectAttribute.Attributeorder);          
            }

            return SQLService.Instance.ExecuteQuery(query, new
            {
                projectid = projectAttribute.ProjectId,
                attributename = projectAttribute.Attributename,
                attributevalue = projectAttribute.Attributevalue,
                attributeorder = projectAttribute.Attributeorder,
                attributeid = projectAttribute.Attributeid
            });
        }

        public static List<ProjectAttribute> GetAll(int projectId)
        {
            if (projectId == 0) return null;

            var query = string.Format(@"select *                          
                          from gemini_projectattributes a
                          where a.projectid = {0} order by attributeorder asc", projectId);

            var result = SQLService.Instance.RunQuery<ProjectAttribute>(query).ToList();
            
            return result;
        }

        public static ProjectAttribute Get(int id)
        {
            if (id == 0) return null;

            var query = string.Format(@"select *                          
                          from gemini_projectattributes 
                          where attributeid = {0} ", id);

            var result = SQLService.Instance.RunQuery<ProjectAttribute>(query).ToList();

            return result.FirstOrDefault();
        }

        public static void Delete(int id)
        {
            var query = string.Format("delete from gemini_projectattributes where attributeid = {0} ", id);

            SQLService.Instance.ExecuteQuery(query);
        }

        public static int GetNextAttributeOrderNumber(int projectId)
        {
            if (projectId == 0) return 0;

            var query = string.Format(@"select ISNULL(max(attributeorder), 0) as attributeorder 
                                        from gemini_projectattributes 
                                        where projectid = {0} ", projectId);

            int result = SQLService.Instance.RunQuery<int>(query).ToList().FirstOrDefault() ;

            return result + 1;
        }
    }
}
