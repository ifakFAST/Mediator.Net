// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ifak.Fast.Mediator.Util
{
    public interface IModelObject
    {
        ObjInfo GetObjectInfo(string moduleID, IEnumerable<IModelObject> parents);
        DataValue GetMemberValueByName(string memberName);
        void SetMemberValue(string memberName, DataValue value);
        void RemoveChildObject(string memberName, IModelObject childObject);
        void AddChildObject(string memberName, DataValue objectToAdd);
    }

    public struct ObjInfo
    {
        public ObjInfo(ObjectRef id, string displayName, string className, IEnumerable<ChildObjectsInMember> childObjects, Variable[]? variables, LocationRef? location) {
            ID = id;
            DisplayName = displayName;
            ClassName = className;
            ChildObjects = childObjects;
            Variables = variables;
            Location = location;
        }
        public ObjectRef ID { get; set; }
        public string DisplayName { get; set; }
        public string ClassName { get; set; }
        public LocationRef? Location { get; set; }
        public IEnumerable<ChildObjectsInMember> ChildObjects { get; }
        public Variable[]? Variables { get; set; }
    }

    public struct ChildObjectsInMember
    {
        public ChildObjectsInMember(string memberName, IModelObject childObject) {
            MemberName = memberName;
            ChildObjects = new IModelObject[] { childObject };
        }

        public ChildObjectsInMember(string memberName, IEnumerable<IModelObject> childObjects) {
            MemberName = memberName;
            ChildObjects = childObjects;
        }
        public string MemberName { get; set; }
        public IEnumerable<IModelObject> ChildObjects { get; set; }
    }


    public abstract class ModelObject : IModelObject
    {
        public ObjInfo GetObjectInfo(string moduleID, IEnumerable<IModelObject> parents) => new ObjInfo(
            ObjectRef.Make(moduleID, GetID(parents)),
            GetDisplayName(parents),
            GetClassName(),
            GetChildObjectsInMember(),
            GetVariablesOrNull(parents),
            GetLocation());

        protected virtual string GetID(IEnumerable<IModelObject> parents) => GetPropertyByNameOrThrow("ID").GetValue(this, null).ToString();

        protected virtual string GetDisplayName(IEnumerable<IModelObject> parents) => GetPropertyByNameOrThrow("Name").GetValue(this, null).ToString();

        protected virtual string GetClassName() => GetType().FullName;

        protected virtual LocationRef? GetLocation() {
            PropertyInfo prop = GetPropertyByNameOrNull("Location");
            if (prop == null) return null;
            var value = prop.GetValue(this, null);
            if (value == null) return null;
            if (value is LocationRef) {
                return (LocationRef)value;
            }
            return null;
        }

        protected virtual Variable[]? GetVariablesOrNull(IEnumerable<IModelObject> parents) => null;

        protected virtual List<ChildObjectsInMember> GetChildObjectsInMember() {
            var res = new List<ChildObjectsInMember>();
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo p in properties) {
                object value = p.GetValue(this, null);
                System.Collections.IEnumerable? list = value as System.Collections.IEnumerable;
                if (list != null && typeof(IEnumerable<IModelObject>).IsAssignableFrom(list.GetType())) {
                    res.Add(new ChildObjectsInMember(p.Name, (IEnumerable<IModelObject>)list));
                }
                else if (value is IModelObject) {
                    res.Add(new ChildObjectsInMember(p.Name, (IModelObject)value));
                }
            }
            return res;
        }

        public DataValue GetMemberValueByName(string memberName) {
            PropertyInfo p = GetPropertyByNameOrThrow(memberName);
            object value = p.GetValue(this, null);
            return DataValue.FromObject(value);
        }

        public void SetMemberValue(string memberName, DataValue value) {
            PropertyInfo p = GetPropertyByNameOrThrow(memberName);
            if (p.PropertyType == typeof(DataValue)) {
                p.SetValue(this, value);
            }
            else if (value.IsEmpty) {
                p.SetValue(this, null);
            }
            else {
                object? decodedValue = value.Object(p.PropertyType);
                p.SetValue(this, decodedValue);
            }
        }

        public void AddChildObject(string memberName, DataValue objectToAdd) {
            PropertyInfo p = GetPropertyByNameOrThrow(memberName);
            System.Collections.IList? list = p.GetValue(this, null) as System.Collections.IList;
            if (list == null) throw new ArgumentException("Member " + memberName + " is not a list.");
            object? decodedValue = objectToAdd.Object(p.PropertyType.GetGenericArguments()[0]);
            list.Add(decodedValue);
        }

        public void RemoveChildObject(string memberName, IModelObject childObject) {
            PropertyInfo p = GetPropertyByNameOrThrow(memberName);
            System.Collections.IList? list = p.GetValue(this, null) as System.Collections.IList;
            if (list == null) throw new ArgumentException("Member " + memberName + " is not a list.");
            list.Remove(childObject);
        }

        private PropertyInfo GetPropertyByNameOrThrow(string name) {
            var res = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (res == null) throw new ArgumentException("No member " + name + " found.");
            return res;
        }

        private PropertyInfo GetPropertyByNameOrNull(string name) {
            var res = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return res;
        }
    }

}
