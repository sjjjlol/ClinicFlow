using System.Linq;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois.Modules.Support
{
    public sealed partial class SupportModule
    {
        internal string Save(string caseId, int caseVersion, int draftVersion, string payloadJson, string operation, string actor)
        {
            var draftCare = clinical.Case(caseId, caseVersion);
            Need(!draftCare.Terminated, "terminated", "诊疗档案已终止");
            Need(payloadJson.Length <= 64000, "invalid_text", "草稿过长", 400);
            try
            {
                using var payload = System.Text.Json.JsonDocument.Parse(payloadJson);
                Need(payload.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object,
                    "invalid_input", "草稿须为表单对象", 400);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new BusinessException("invalid_input", "草稿格式无效", 400);
            }
            var draft = drafts.SingleOrDefault(x =>
                x.CaseId == caseId && x.Actor == actor && x.Operation == operation);
            if (draft == null)
            {
                Need(draftVersion == 0, "stale_version", "草稿已提交，请重新打开");
                draft = new OisDraft { CaseId = caseId, Actor = actor, Operation = operation };
                drafts.Add(draft);
            }
            else
            {
                Need(draft.Version == draftVersion, "stale_version", "草稿已更新，请刷新");
                draft.Version++;
            }
            draft.Payload = payloadJson;
            draft.CaseVersion = caseVersion;
            return draft.Id;
        }
    }
}
