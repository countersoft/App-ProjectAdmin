using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectAdmin.Models
{
    public class ProjectAttribute
    {
        public decimal Attributeid { get; set; }
        public decimal ProjectId { get; set; }
        public string Attributename { get; set; }
        public string Attributevalue { get; set; }
        public int Attributeorder { get; set; }
        public DateTime Created { get; set; }
    }
}
