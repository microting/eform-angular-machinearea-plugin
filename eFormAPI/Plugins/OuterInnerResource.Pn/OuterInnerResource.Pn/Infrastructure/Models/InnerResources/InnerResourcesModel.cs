﻿using System.Collections.Generic;

namespace OuterInnerResource.Pn.Infrastructure.Models.InnerResources
{
    public class InnerResourcesModel
    {
        public int Total { get; set; }
        public List<InnerResourceModel> InnerResourceList { get; set; }
        public string Name { get; set; }
    }
}