// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator
{
    public abstract class AttributeBase : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class Browseable : AttributeBase
    {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class Ignore : AttributeBase
    {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ContainsNestedModel : AttributeBase {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class Category : AttributeBase
    {
        public string Name { get; set; }

        public Category(string category) {
            Name = category;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultCategory : AttributeBase
    {
        public string Name { get; set; }

        public DefaultCategory(string category) {
            Name = category;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class IdPrefix : AttributeBase
    {
        public string Value { get; set; }

        public IdPrefix(string value) {
            Value = value;
        }
    }

}
