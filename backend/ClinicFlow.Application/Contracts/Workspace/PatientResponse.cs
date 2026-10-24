using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class PatientResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Identifier { get; set; } = "";
    }
}
