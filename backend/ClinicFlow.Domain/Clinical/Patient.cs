using System;
using static ClinicFlow.Ois.DomainRules;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow
{
    public class Patient
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = "";
        public string Identifier { get; internal set; } = "";
        public static Patient Register(int id, string name, string identifier) => new Patient { Id = id, Name = name, Identifier = identifier };
    }


}
