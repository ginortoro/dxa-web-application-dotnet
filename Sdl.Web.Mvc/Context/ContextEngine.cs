﻿using System;
using System.Collections.Generic;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;

namespace Sdl.Web.Mvc.Context
{
    /// <summary>
    /// Provides access to the context claims as provided by the Context Engine.
    /// </summary>
    /// <remarks>
    /// Based on https://github.com/NunoLinhares/TridionContextEngineWrapper/tree/master/Sdl.Tridion.Context
    /// </remarks>
    public class ContextEngine
    {
        private readonly IDictionary<string, object> _claims;
        private readonly IDictionary<Type, ContextClaims> _stronglyTypedClaims = new Dictionary<Type, ContextClaims>();
        private string _deviceFamily;

        /// <summary>
        /// Initializes a new <see cref="ContextEngine"/> instance.
        /// </summary>
        /// <remarks><see cref="ContextEngine"/> should not be constructed directly, but through <see cref="Sdl.Web.Mvc.Configuration.WebRequestContext.ContextEngine"/>.</remarks>
        internal ContextEngine()
        {
            using (new Tracer())
            {
                // For now, we get all context claims (for all aspects) in one go:
                _claims = SiteConfiguration.ContextClaimsProvider.GetContextClaims(null);
            }
        }

        /// <summary>
        /// Gets strongly typed claims of a given type (for a specific aspect).
        /// </summary>
        /// <typeparam name="T">
        /// The strongly typed claims class. Must be a subclass of <see cref="ContextClaims"/>. 
        /// For example: <see cref="DeviceClaims"/>, <see cref="BrowserClaims"/> or <see cref="OperatingSystemClaims"/>.
        /// </typeparam>
        /// <returns>An instance of the strongly typed claims class which can be used to access context claims for a specific aspect.</returns>
        public T GetClaims<T>() 
            where T: ContextClaims, new()
        {
            using (new Tracer(typeof(T)))
            {
                ContextClaims stronglyTypedClaims;
                if (!_stronglyTypedClaims.TryGetValue(typeof(T), out stronglyTypedClaims))
                {
                    stronglyTypedClaims = new T();
                    stronglyTypedClaims.SetClaims(_claims);
                    _stronglyTypedClaims.Add(typeof(T), stronglyTypedClaims);
                }
                return (T)stronglyTypedClaims;
            }
        }

        /// <summary>
        /// Gets the device family (an aggregated device claim determined from other context claims).
        /// </summary>
        /// <returns>A string representing the device family.</returns>
        public string DeviceFamily
        {
            get
            {
                using (new Tracer())
                {
                    if (_deviceFamily != null)
                    {
                        return _deviceFamily;
                    }

                    _deviceFamily = SiteConfiguration.ContextClaimsProvider.GetDeviceFamily();
                    if (_deviceFamily == null)
                    {
                        // Defaults
                        DeviceClaims device = GetClaims<DeviceClaims>();
                        if (!device.IsMobile && !device.IsTablet) _deviceFamily = "desktop";
                        if (device.IsTablet) _deviceFamily = "tablet";
                        if (device.IsMobile && !device.IsTablet)
                        {
                            _deviceFamily = device.DisplayWidth > 319 ? "smartphone" : "featurephone";
                        }
                    }
                    return _deviceFamily;
                }
            }
        }

        #region Obsolete
        [Obsolete("Deprecated in DXA 1.1. Use GetClaims<BrowserClaims>() instead.")]
        public BrowserClaims Browser
        {
            get
            {
                return GetClaims<BrowserClaims>();
            }
        }

        [Obsolete("Deprecated in DXA 1.1. Use GetClaims<DeviceClaims>() instead.")]
        public DeviceClaims Device
        {
            get
            {
                return GetClaims<DeviceClaims>();
            }
        }

        [Obsolete("Deprecated in DXA 1.1. Use GetClaims<OperatingSystemClaims>() instead.")]
        public OperatingSystemClaims Os
        {
            get
            {
                return GetClaims<OperatingSystemClaims>();
            }
        }
        #endregion
    }
}
