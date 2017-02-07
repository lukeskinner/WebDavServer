﻿// <copyright file="SimpleConvertingProperty.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FubarDev.WebDavServer.Props.Converters;

using JetBrains.Annotations;

namespace FubarDev.WebDavServer.Props
{
    public abstract class SimpleConvertingProperty<T> : SimpleTypedProperty<T>
    {
        protected SimpleConvertingProperty([NotNull] XName name, int cost, [NotNull] IPropertyConverter<T> converter, params XName[] alternativeNames)
            : base(name, cost, alternativeNames)
        {
            Converter = converter;
        }

        [NotNull]
        protected IPropertyConverter<T> Converter { get; }

        public override async Task<XElement> GetXmlValueAsync(CancellationToken ct)
        {
            var result = await GetValueAsync(ct).ConfigureAwait(false);
            return Converter.ToElement(Name, result);
        }

        public override Task SetXmlValueAsync(XElement element, CancellationToken ct)
        {
            return SetValueAsync(Converter.FromElement(element), ct);
        }
    }
}
