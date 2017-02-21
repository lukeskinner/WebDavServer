﻿// <copyright file="CodedUrlParser.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;

using FubarDev.WebDavServer.Utils;

using JetBrains.Annotations;

namespace FubarDev.WebDavServer.Model.Headers
{
    public static class CodedUrlParser
    {
        public static Uri Parse([NotNull] string source)
        {
            var src = new StringSource(source);
            Uri stateToken;
            if (!TryParse(src, out stateToken))
                throw new FormatException("No Coded-URL found");
            if (!src.Empty)
                throw new FormatException("Unknown content after Coded-URL");
            return stateToken;
        }

        public static bool TryParse([NotNull] string source, out Uri stateToken)
        {
            var src = new StringSource(source);
            if (!TryParse(src, out stateToken))
                return false;
            return src.Empty;
        }

        internal static bool TryParse([NotNull] StringSource source, out Uri stateToken)
        {
            if (!source.AdvanceIf("<"))
            {
                stateToken = null;
                return false;
            }

            // Coded-URL found
            var codedUrl = source.GetUntil('>');
            if (codedUrl == null)
                throw new ArgumentException($"{source.Remaining} is not a valid Coded-URL (not ending with '>')", nameof(source));
            source.Advance(1);
            stateToken = new Uri(codedUrl, UriKind.RelativeOrAbsolute);
            return true;
        }
    }
}