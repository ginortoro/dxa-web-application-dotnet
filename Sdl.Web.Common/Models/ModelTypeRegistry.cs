﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Compilation;
using System.Web.UI.WebControls;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Mapping;

namespace Sdl.Web.Common.Models
{
    /// <summary>
    /// Represents a Registry of View Model Types and associated Semantics.
    /// </summary>
    public static class ModelTypeRegistry
    {
        private static readonly IDictionary<MvcData, Type> _viewToModelTypeMapping = new Dictionary<MvcData, Type>();
        private static readonly IDictionary<Type, SemanticInfo> _modelTypeToSemanticInfoMapping = new Dictionary<Type, SemanticInfo>();
        private static readonly IDictionary<string, ISet<Type>> _semanticTypeToModelTypesMapping = new Dictionary<string, ISet<Type>>();

        private class SemanticInfo
        {
            internal readonly IDictionary<string, string> PrefixMappings = new Dictionary<string, string>();
            internal readonly IList<string> PublicSemanticTypes = new List<string>();
            internal readonly IList<string> MappedSemanticTypes = new List<string>(); 
            internal readonly IDictionary<string, IList<string>> SemanticProperties = new Dictionary<string, IList<string>>();
        }

        /// <summary>
        /// Registers a View Model and associated View.
        /// </summary>
        /// <param name="viewData">The data for the View to register or <c>null</c> if only the Model Type is to be registered.</param>
        /// <param name="modelType">The model Type used by the View.</param>
        public static void RegisterViewModel(MvcData viewData, Type modelType)
        {
            using (new Tracer(viewData, modelType))
            {
                lock (_viewToModelTypeMapping)
                {
                    if (viewData != null)
                    {
                        if (_viewToModelTypeMapping.ContainsKey(viewData))
                        {
                            Log.Warn("View '{0}' registered multiple times.", viewData);
                            return;
                        }
                        _viewToModelTypeMapping.Add(viewData, modelType);
                    }

                    if (!_modelTypeToSemanticInfoMapping.ContainsKey(modelType))
                    {
                        RegisterModelType(modelType);
                    }
                }
            }
        }

        /// <summary>
        /// Registers a View Model mapping by compiling a given view file and obtaining its model type.
        /// </summary>
        /// <param name="viewData">The data for the View to register.</param>
        /// <param name="viewVirtualPath">The (virtual) path to the View file.</param>
        public static void RegisterViewModel(MvcData viewData, string viewVirtualPath)
        {
            using (new Tracer(viewData, viewVirtualPath))
            {
                lock (_viewToModelTypeMapping)
                {
                    if (_viewToModelTypeMapping.ContainsKey(viewData))
                    {
                        Log.Warn("View '{0}' registered multiple times. Virtual Path: '{1}'", viewData, viewVirtualPath);
                        return;
                    }

                    try
                    {
                        Type compiledViewType = BuildManager.GetCompiledType(viewVirtualPath);
                        if (!compiledViewType.BaseType.IsGenericType)
                        {
                            throw new DxaException("View is not strongly typed. Please ensure you use the @model directive.");
                        }
                        RegisterViewModel(viewData, compiledViewType.BaseType.GetGenericArguments()[0]);
                    }
                    catch (Exception ex)
                    {
                        throw new DxaException(string.Format("Error occurred while compiling View '{0}'", viewVirtualPath), ex);
                    }
                }
            }
        }

        /// <summary>
        /// Get the View Model Type for a given View.
        /// </summary>
        /// <param name="viewData">The data for the View.</param>
        /// <returns>The View Model Type.</returns>
        public static Type GetViewModelType(MvcData viewData)
        {
            Type modelType;
            MvcData bareMvcData = new MvcData
            {
                AreaName = viewData.AreaName,
                ControllerName = viewData.ControllerName,
                ViewName = viewData.ViewName
            };
            if (!_viewToModelTypeMapping.TryGetValue(bareMvcData, out modelType))
            {
                throw new DxaException(
                    string.Format("No View Model registered for View '{0}'. Check that you have registered this View in the '{1}' area registration.", viewData, viewData.AreaName)
                    );
            }
            return modelType;
        }

        /// <summary>
        /// Gets the semantic types (and prefix mappings) for a given Model Type.
        /// </summary>
        /// <param name="modelType">The Model Type.</param>
        /// <param name="prefixMappings">The prefix mappings for the prefixes used by the types.</param>
        /// <returns>The semantic types.</returns>
        public static string[] GetSemanticTypes(Type modelType, out IDictionary<string, string> prefixMappings)
        {
            // No Tracer here to reduce trace noise.
            SemanticInfo semanticInfo = GetSemanticInfo(modelType);
            prefixMappings = semanticInfo.PrefixMappings;
            return semanticInfo.PublicSemanticTypes.ToArray();
        }

        /// <summary>
        /// Gets the semantic property names for a given Model Type and property name.
        /// </summary>
        /// <param name="modelType">The Model Type.</param>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The semantic property names or <c>null</c> if no semantic property names have been registered for the given property.</returns>
        public static string[] GetSemanticPropertyNames(Type modelType, string propertyName)
        {
            // No Tracer here to reduce trace noise.
            SemanticInfo semanticInfo = GetSemanticInfo(modelType);

            IList<string> semanticPropertyNames;
            if (semanticInfo.SemanticProperties.TryGetValue(propertyName, out semanticPropertyNames))
            {
                return semanticPropertyNames.ToArray();
            }
            return null;
        }

        /// <summary>
        /// Gets the Model Types mapped to a given semantic type name.
        /// </summary>
        /// <param name="semanticTypeName">The semantic type name qualified with vocabulary ID.</param>
        /// <returns>The mapped model types or <c>null</c> if no Model types are registered for the given semantic type name.</returns>
        public static IEnumerable<Type> GetMappedModelTypes(string semanticTypeName)
        {
            ISet<Type> mappedModelTypes;
            _semanticTypeToModelTypesMapping.TryGetValue(semanticTypeName, out mappedModelTypes);
            return mappedModelTypes;
        }

        private static SemanticInfo RegisterModelType(Type modelType)
        {
            using (new Tracer(modelType))
            {
                SemanticInfo semanticInfo = ExtractSemanticInfo(modelType);
                _modelTypeToSemanticInfoMapping.Add(modelType, semanticInfo);

                foreach (string semanticTypeName in semanticInfo.MappedSemanticTypes)
                {
                    ISet<Type> mappedModelTypes;
                    if (!_semanticTypeToModelTypesMapping.TryGetValue(semanticTypeName, out mappedModelTypes))
                    {
                        mappedModelTypes = new HashSet<Type>();
                        _semanticTypeToModelTypesMapping.Add(semanticTypeName, mappedModelTypes);
                    }
                    mappedModelTypes.Add(modelType);
                }

                if (semanticInfo.PublicSemanticTypes.Any())
                {
                    Log.Debug("Model type '{0}' has semantic type(s) '{1}'.", modelType.FullName, string.Join(" ", semanticInfo.PublicSemanticTypes));
                    foreach (KeyValuePair<string, IList<string>> kvp in semanticInfo.SemanticProperties)
                    {
                        Log.Debug("\tRegistered property '{0}' as semantic property '{1}'", kvp.Key, string.Join(" ", kvp.Value));
                    }
                }

                return semanticInfo;
            }
        }

        private static SemanticInfo GetSemanticInfo(Type modelType)
        {
            SemanticInfo semanticInfo;
            if (!_modelTypeToSemanticInfoMapping.TryGetValue(modelType, out semanticInfo))
            {
                // Just-In-Time model type registration.
                semanticInfo = RegisterModelType(modelType);
            }
            return semanticInfo;
        }

        private static SemanticInfo ExtractSemanticInfo(Type modelType)
        {
            SemanticInfo semanticInfo = new SemanticInfo();
            
            // Built-in semantic type mapping
            string bareTypeName = modelType.Name.Split('`')[0]; // Type name without generic type parameters (if any)
            semanticInfo.MappedSemanticTypes.Add(SemanticMapping.GetQualifiedTypeName(bareTypeName));

            // Extract semantic info from SemanticEntity attributes on the Model Type.
            foreach (SemanticEntityAttribute attribute in modelType.GetCustomAttributes(true).Where(a => a is SemanticEntityAttribute))
            {
                semanticInfo.MappedSemanticTypes.Add(SemanticMapping.GetQualifiedTypeName(attribute.EntityName, attribute.Vocab));

                if (!attribute.Public || string.IsNullOrEmpty(attribute.Prefix))
                    continue;

                string prefix = attribute.Prefix;
                string registeredVocab;
                if (semanticInfo.PrefixMappings.TryGetValue(prefix, out registeredVocab))
                {
                    // Prefix mapping already exists; must match.
                    if (attribute.Vocab != registeredVocab)
                    {
                        throw new DxaException(
                            string.Format("Attempt to use semantic prefix '{0}' for vocabulary '{1}', but is is already used for vocabulary '{2}", 
                                prefix, attribute.Vocab, registeredVocab)
                            );
                    }
                }
                else
                {
                    semanticInfo.PrefixMappings.Add(prefix, attribute.Vocab);
                }

                semanticInfo.PublicSemanticTypes.Add(String.Format("{0}:{1}", prefix, attribute.EntityName));
            }

            // Extract semantic info from SemanticEntity attributes on the Model Type's properties
            foreach (MemberInfo memberInfo in modelType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (SemanticPropertyAttribute attribute in memberInfo.GetCustomAttributes(true).Where(a => a is SemanticPropertyAttribute))
                {
                    if (string.IsNullOrEmpty(attribute.PropertyName))
                    {
                        // Skip properties without name.
                        continue;
                    }
                    string[] semanticPropertyNameParts = attribute.PropertyName.Split(':');
                    if (semanticPropertyNameParts.Length < 2)
                    {
                        // Skip property names without prefix.
                        continue;
                    }
                    string prefix = semanticPropertyNameParts[0];
                    if (!semanticInfo.PrefixMappings.ContainsKey(prefix))
                    {
                        // Skip property names with prefix which is not declared as public prefix on the type.
                        continue;
                    }

                    IList<string> semanticPropertyNames;
                    if (!semanticInfo.SemanticProperties.TryGetValue(memberInfo.Name, out semanticPropertyNames))
                    {
                        semanticPropertyNames = new List<string>();
                        semanticInfo.SemanticProperties.Add(memberInfo.Name, semanticPropertyNames);
                    }
                    semanticPropertyNames.Add(attribute.PropertyName);
                }
            }

            return semanticInfo;
        }

    }
}
