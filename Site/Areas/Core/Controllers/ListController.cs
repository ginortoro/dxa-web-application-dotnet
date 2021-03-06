﻿using System.Linq;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Controllers;
using System.Web.Mvc;

namespace Sdl.Web.Site.Areas.Core.Controllers
{
    public class ListController : EntityController
    {
        /// <summary>
        /// Populate/Map and render a list entity model
        /// </summary>
        /// <param name="entity">The list entity model</param>
        /// <param name="containerSize">The size (in grid units) of the container the entity is in</param>
        /// <returns>Rendered list entity model</returns>
        [HandleSectionError(View = "SectionError")]
        public ActionResult List(EntityModel entity, int containerSize = 0)
        {
            // The List action is effectively just an alias for the general Entity action (we keep it for backward compatibility).
            return Entity(entity, containerSize);
        }

        protected override ViewModel EnrichModel(ViewModel sourceModel)
        {
            ContentList<Teaser> model = base.EnrichModel(sourceModel) as ContentList<Teaser>;
            if (model == null || model.ItemListElements.Any())
            {
                return model;
            }

            //we need to run a query to populate the list
            int start = GetRequestParameter<int>("start");
            if (model.Id == Request.Params["id"])
            {
                //we only take the start from the query string if there is also an id parameter matching the model entity id
                //this means that we are sure that the paging is coming from the right entity (if there is more than one paged list on the page)
                model.CurrentPage = (start / model.PageSize) + 1;
                model.Start = start;
            }
            ContentProvider.PopulateDynamicList(model, WebRequestContext.Localization);

            return model;
        }
    }
}
