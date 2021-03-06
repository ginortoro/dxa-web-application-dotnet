﻿using System;
using DD4T.ContentModel.Factories;
using DD4T.Factories;

namespace Sdl.Web.Tridion.Mapping
{
    internal class ExtensionlessLinkFactory : LinkFactory, ILinkFactory
    {
        private static string RemoveExtension(string url)
        {
            if (url != null)
            {
                int pos = url.LastIndexOf(".", StringComparison.Ordinal);
                if (pos > url.LastIndexOf("/", StringComparison.Ordinal))
                {
                    url = url.Substring(0, pos);
                }
            }
            return url;
        }
        
        string ILinkFactory.ResolveLink(string sourcePageUri, string componentUri, string excludeComponentTemplateUri)
        {
            return  RemoveExtension(ResolveLink(sourcePageUri, componentUri, excludeComponentTemplateUri));
        }
        
        string ILinkFactory.ResolveLink(string componentUri)
        {
            return  RemoveExtension(ResolveLink(componentUri));
        }
    }
}