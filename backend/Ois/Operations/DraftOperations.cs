using System.Linq;

namespace ClinicFlow.Ois.Operations
{
    internal sealed class DraftOperations
    {
        private readonly OisState state;
        internal DraftOperations(OisState state) => this.state = state;
        internal string Save(string caseId, int caseVersion, int draftVersion, string payloadJson, string operation, string actor)
        {
            var draftCare = state.Current(state.Cases, caseId, caseVersion);
            state.Need(!draftCare.Terminated, "terminated", "诊疗档案已终止");
            state.Need(payloadJson.Length <= 64000, "invalid_text", "草稿过长", 400);
            try
            {
                using var payload = System.Text.Json.JsonDocument.Parse(payloadJson);
                state.Need(payload.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object,
                    "invalid_input", "草稿须为表单对象", 400);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new BusinessException("invalid_input", "草稿格式无效", 400);
            }
            var draft = state.Drafts.SingleOrDefault(x =>
                x.CaseId == caseId && x.Actor == actor && x.Operation == operation);
            if (draft == null)
            {
                state.Need(draftVersion == 0, "stale_version", "草稿已提交，请重新打开");
                draft = new OisDraft { CaseId = caseId, Actor = actor, Operation = operation };
                state.Drafts.Add(draft);
            }
            else
            {
                state.Need(draft.Version == draftVersion, "stale_version", "草稿已更新，请刷新");
                draft.Version++;
            }
            draft.Payload = payloadJson;
            draft.CaseVersion = caseVersion;
            return draft.Id;
        }
    }
}
