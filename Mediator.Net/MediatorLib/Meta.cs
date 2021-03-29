// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator
{
    public class MetaInfos
    {
        public MetaInfos() { }

        public MetaInfos(ClassInfo[] classes, StructInfo[] structs, EnumInfo[] enums) {
            Classes = classes ?? new ClassInfo[0];
            Structs = structs ?? new StructInfo[0];
            Enums = enums ?? new EnumInfo[0];
        }

        public ClassInfo[] Classes { get; set; } = new ClassInfo[0];
        public StructInfo[] Structs { get; set; } = new StructInfo[0];
        public EnumInfo[] Enums { get; set; } = new EnumInfo[0];
    }

    public class StructInfo
    {
        public string FullName { get; set; } = "";
        public string Name { get; set; } = "";
        public List<SimpleMember> Member { get; set; } = new List<SimpleMember>();
    }

    public class ClassInfo
    {
        public string IdPrefix { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsRoot { get; set; } = false;
        public bool IsAbstract { get; set; } = false;
        public string BaseClassName { get; set; } = "";
        public List<SimpleMember> SimpleMember { get; set; } = new List<SimpleMember>();
        public List<ObjectMember> ObjectMember { get; set; } = new List<ObjectMember>();
    }

    public class SimpleMember
    {
        public SimpleMember() { }

        public SimpleMember(string name, DataType type, string typeConstraints, Dimension dimension, DataValue? defaultValue, bool browseable, string category) {
            if (name == null) throw new ArgumentNullException(nameof(name), nameof(name) + "may not be null");
            Name = name;
            Type = type;
            TypeConstraints = typeConstraints ?? "";
            Dimension = dimension;
            DefaultValue = defaultValue;
            Browseable = browseable;
            Category = category;
        }

        public string Name { get; set; } = "";

        public DataType Type { get; set; }

        public string TypeConstraints { get; set; } = "";

        public Dimension Dimension { get; set; } = Dimension.Scalar;

        public DataValue? DefaultValue { get; set; }

        /// <summary>
        /// Can be specified with the Mediator.Browseable attribute.
        /// </summary>
        public bool Browseable { get; set; }

        /// <summary>
        /// Can be specified with the Mediator.Category attribute.
        /// </summary>
        public string Category { get; set; } = "";
    }

    public class ObjectMember
    {
        public ObjectMember() { }

        public ObjectMember(string name, string className, Dimension dimension, bool browseable) {
            Name = name ?? throw new ArgumentNullException(nameof(name), nameof(name) + "may not be null");
            ClassName = className ?? throw new ArgumentNullException(nameof(className), nameof(className) + "may not be null");
            Dimension = dimension;
            Browseable = browseable;
        }

        public string Name { get; set; } = "";
        public string ClassName { get; set; } = "";
        public Dimension Dimension { get; set; } = Dimension.Scalar;

        /// <summary>
        /// Can be specified with the Mediator.Browseable attribute.
        /// </summary>
        public bool Browseable { get; set; }
    }

    public enum Dimension
    {
        Scalar,
        Optional,
        Array
    }

    public class EnumInfo
    {
        public string FullName { get; set; } = "";
        public string Name { get; set; } = "";
        public List<EnumValue> Values { get; set; } = new List<EnumValue>();
    }

    public class EnumValue {

        public EnumValue(string name, string? description = null) {
            Name = name ?? throw new ArgumentNullException(nameof(name), nameof(name) + "may not be null");
            Description = description == null || string.IsNullOrEmpty(description) ? name : description;
        }

        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
