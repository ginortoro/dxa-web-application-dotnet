﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using HtmlAgilityPack;
using Sdl.Web.Common;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;

namespace Sdl.Web.Mvc.Html
{
    /// <summary>
    /// Utility class for methods which generate semantic markup (HTML/RDFa attributes) for use by machine processing (search engines, XPM etc.)
    /// </summary>
    public static class Markup
    {
        private const string XpmMarkupHtmlAttrName = "data-xpm";
        private const string XpmMarkupXPath = "//*[@" + XpmMarkupHtmlAttrName + "]";
        private const string XpmFieldMarkup = "<!-- Start Component Field: {{\"XPath\":\"{0}\"}} -->";

        private class XpmMarkupMap : Dictionary<string, string>
        {
            private int _index;

            internal string AddXpmMarkup(string xpmMarkup)
            {
                string index = Convert.ToString(_index++);
                Add(index, xpmMarkup);
                return index;
            }

            internal static XpmMarkupMap Current 
            {
                get
                {
                    const string httpContextItemName = "XpmMarkupMap";
                    HttpContext httpContext = HttpContext.Current;
                    XpmMarkupMap result = httpContext.Items[httpContextItemName] as XpmMarkupMap;
                    if (result == null)
                    {
                        result = new XpmMarkupMap();
                        httpContext.Items[httpContextItemName] = result;
                    }
                    return result;
                }
            }
        }

        #region Obsolete Public API
#pragma warning disable 618

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Region.
        /// </summary>
        /// <param name="region">The Region.</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        [Obsolete("Deprecated in DXA 1.1. Use @Html.DxaRegionMarkup instead.")]
        public static MvcHtmlString Region(IRegion region)
        {
            return RenderRegionAttributes((RegionModel) region);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Entity.
        /// </summary>
        /// <param name="entity">The Entity.</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        [Obsolete("Deprecated in DXA 1.1. Use @Html.DxaEntityMarkup instead.")]
        public static MvcHtmlString Entity(IEntity entity)
        {
            return RenderEntityAttributes((EntityModel) entity);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property of a given Entity.
        /// </summary>
        /// <param name="entity">The Entity which contains the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        [Obsolete("Deprecated in DXA 1.1. Use @Html.DxaPropertyMarkup instead.")]
        public static MvcHtmlString Property(IEntity entity, string propertyName, int index = 0)
        {
            return RenderPropertyAttributes((EntityModel) entity, propertyName, index);
        }
        
#pragma warning restore 618
        #endregion

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Entity Model.
        /// </summary>
        /// <param name="entityModel">The Entity Model.</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        internal static MvcHtmlString RenderEntityAttributes(EntityModel entityModel)
        {
            string markup = String.Empty;

            IDictionary<string, string> prefixMappings;
            string[] semanticTypes = ModelTypeRegistry.GetSemanticTypes(entityModel.GetType(), out prefixMappings);
            if (semanticTypes.Any())
            {
                markup = String.Format(
                    "prefix=\"{0}\" typeof=\"{1}\"",
                    String.Join(" ", prefixMappings.Select(pm => String.Format("{0}: {1}", pm.Key, pm.Value))), 
                    String.Join(" ", semanticTypes)
                    );
            }

            if (WebRequestContext.IsPreview)
            {
                string xpmMarkupAttr = RenderXpmMarkupAttribute(entityModel);
                if (String.IsNullOrEmpty(markup))
                {
                    markup = xpmMarkupAttr;
                }
                else
                {
                    markup += " " + xpmMarkupAttr;
                }
            }

            return new MvcHtmlString(markup);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property of a given Entity Model.
        /// </summary>
        /// <param name="entityModel">The Entity Model which contains the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        internal static MvcHtmlString RenderPropertyAttributes(EntityModel entityModel, string propertyName, int index = 0)
        {
            PropertyInfo propertyInfo = entityModel.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                throw new DxaException(
                    String.Format("Entity Type '{0}' does not have a property named '{1}'.", entityModel.GetType().Name, propertyName)
                    );
            }
            return RenderPropertyAttributes(entityModel, propertyInfo, index);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property of a given Entity Model.
        /// </summary>
        /// <param name="entityModel">The Entity Model which contains the property.</param>
        /// <param name="propertyInfo">The reflected property info.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        internal static MvcHtmlString RenderPropertyAttributes(EntityModel entityModel, MemberInfo propertyInfo, int index = 0)
        {
            string markup = String.Empty;
            string propertyName = propertyInfo.Name;

            string[] semanticPropertyNames = ModelTypeRegistry.GetSemanticPropertyNames(propertyInfo.DeclaringType, propertyName);
            if (semanticPropertyNames != null && semanticPropertyNames.Any())
            {
                markup = String.Format("property=\"{0}\"", String.Join(" ", semanticPropertyNames));
            }

            if (WebRequestContext.IsPreview)
            {
                string xpmMarkupAttr = RenderXpmMarkupAttribute(entityModel, propertyName, index);
                if (String.IsNullOrEmpty(markup))
                {
                    markup = xpmMarkupAttr;
                }
                else
                {
                    markup += " " + xpmMarkupAttr;
                }
            }

            Log.Debug("Rendered markup for Entity [{0}] Property '{1}': {2}", entityModel, propertyName, markup);

            return new MvcHtmlString(markup);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Region Model.
        /// </summary>
        /// <param name="regionModel">The Region Model.</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        internal static MvcHtmlString RenderRegionAttributes(RegionModel regionModel)
        {
            // TODO: "Region" is not a valid semantic type!
            string markup = String.Format("typeof=\"{0}\" resource=\"{1}\"", "Region", regionModel.Name);

            if (WebRequestContext.IsPreview)
            {
                markup += " " + RenderXpmMarkupAttribute(regionModel);
            }

            return new MvcHtmlString(markup);
        }


        /// <summary>
        /// Renders a temporary HTML attribute containing the XPM markup for a given View Model or property.
        /// </summary>
        /// <seealso cref="TransformXpmMarkupAttributes"/>
        private static string RenderXpmMarkupAttribute(ViewModel viewModel, string propertyName = null, int index = 0)
        {
            string xpmMarkup;
            if (propertyName == null)
            {
                // Region/Entity markup
                xpmMarkup = viewModel.GetXpmMarkup(WebRequestContext.Localization);
                if (String.IsNullOrEmpty(xpmMarkup))
                {
                    return String.Empty;
                }
            }
            else
            {
                // Property markup
                EntityModel entityModel = (EntityModel) viewModel;
                string xpath;
                if (entityModel.XpmPropertyMetadata != null && entityModel.XpmPropertyMetadata.TryGetValue(propertyName, out xpath))
                {
                    string predicate = xpath.EndsWith("]") ? String.Empty : String.Format("[{0}]", index + 1);
                    xpmMarkup = String.Format(XpmFieldMarkup, HttpUtility.HtmlAttributeEncode(xpath + predicate));
                }
                else
                {
                    return String.Empty;
                }
            }

            // Instead of jamming the entire XPM markup in an HTML attribute, we only put in a reference to the XPM markup.
            string xpmMarkupRef = XpmMarkupMap.Current.AddXpmMarkup(xpmMarkup);

            return String.Format("{0}=\"{1}\"", XpmMarkupHtmlAttrName, xpmMarkupRef);
        }

        /// <summary>
        /// Transforms XPM markup contained in HTML attributes to HTML comments inside the HTML elements.
        /// </summary>
        /// <param name="htmlFragment">The HTML fragment to tranform.</param>
        /// <returns>The transformed HTML fragment.</returns>
        internal static string TransformXpmMarkupAttributes(string htmlFragment)
        {
            //HTML Agility pack drops closing option tags for some reason (bug?)
            HtmlNode.ElementsFlags.Remove("option");

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(String.Format("<html>{0}</html>", htmlFragment));
            HtmlNode rootElement = htmlDoc.DocumentNode.FirstChild;
            HtmlNodeCollection elementsWithXpmMarkup = rootElement.SelectNodes(XpmMarkupXPath);
            if (elementsWithXpmMarkup != null)
            {
                XpmMarkupMap xpmMarkupMap = XpmMarkupMap.Current;
                foreach (HtmlNode elementWithXpmMarkup in elementsWithXpmMarkup)
                {
                    string xpmMarkupRef = ReadAndRemoveAttribute(elementWithXpmMarkup, XpmMarkupHtmlAttrName);
                    string xpmMarkup = xpmMarkupMap[xpmMarkupRef];

                    if (String.IsNullOrEmpty(xpmMarkup))
                    {
                        continue;
                    }

                    HtmlCommentNode xpmMarkupNode = htmlDoc.CreateComment(xpmMarkup);
                    elementWithXpmMarkup.ChildNodes.Insert(0, xpmMarkupNode);

                }
            }
            return rootElement.InnerHtml;
        }


        private static string ReadAndRemoveAttribute(HtmlNode htmlElement, string attributeName)
        {
            if (!htmlElement.Attributes.Contains(attributeName))
            {
                return string.Empty;
            }

            HtmlAttribute attr = htmlElement.Attributes[attributeName];
            htmlElement.Attributes.Remove(attr);
            return HttpUtility.HtmlDecode(attr.Value);
        }
    }
}
