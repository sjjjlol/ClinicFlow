import { useEffect, useState } from "react";
import { api } from "./api";

type Study = {
  studyInstanceUid: string;
  description: string;
  studyDate: string;
  modalities: string;
};
type Instance = {
  seriesInstanceUid: string;
  sopInstanceUid: string;
  seriesDescription: string;
  modality: string;
  instanceNumber: string;
};
type ImagingState = {
  identity: {
    source: string;
    externalPatientId: string;
    issuer: string;
  } | null;
  links: {
    studyInstanceUid: string;
    description: string;
    linkedBy: string;
    linkedUtc: string;
  }[];
  audit: {
    id: number;
    action: string;
    actor: string;
    atUtc: string;
    correlationId: string;
    studyInstanceUid: string;
  }[];
};

export default function ImagingPanel({
  appointmentId,
}: {
  appointmentId: string;
}) {
  const root = `/api/imaging/${appointmentId}`;
  const [state, setState] = useState<ImagingState | null>(null);
  const [candidates, setCandidates] = useState<Study[] | null>(null);
  const [selected, setSelected] = useState<{
    uid: string;
    instances: Instance[];
  } | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  async function refresh() {
    setState(await api<ImagingState>(root + "/"));
  }
  useEffect(() => {
    let alive = true;
    api<ImagingState>(root + "/")
      .then((s) => {
        if (alive) setState(s);
      })
      .catch((e) => {
        if (alive) setError(e.message);
      });
    return () => {
      alive = false;
    };
  }, [root]);
  async function act(work: () => Promise<void>) {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await work();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="imaging-panel" aria-label="就诊前影像资料">
      <div className="imaging-heading">
        <h3>
          就诊前影像资料 <small>DICOM</small>
        </h3>
        <button disabled={busy} onClick={() => void act(refresh)}>
          刷新影像关联
        </button>
      </div>
      <p className="muted">
        关联患者已有检查，供本次就诊参考。演示图像为合成数据。
      </p>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      {!state ? (
        !error && <p>正在加载影像关联…</p>
      ) : (
        <>
          {state.identity ? (
            <p className="imaging-identity">
              外部身份：{state.identity.externalPatientId} ·{" "}
              {state.identity.issuer}{" "}
              <small>来源：{state.identity.source}</small>
            </p>
          ) : (
            <p className="notice">
              该患者尚未配置外部影像身份映射，请联系系统维护人员。
            </p>
          )}
          <button
            disabled={busy || !state.identity}
            onClick={() =>
              void act(async () => {
                const result = await api<{
                  items: Study[];
                  truncated: boolean;
                }>(root + "/search");
                setCandidates(result.items);
                if (result.truncated)
                  setNotice("仅显示前100项检查，完整历史需在影像系统查询。");
              })
            }
          >
            {busy ? "正在处理…" : "查询患者已有影像"}
          </button>
          {candidates && (
            <div className="imaging-candidates">
              <h4>可关联检查</h4>
              {candidates.length === 0 && <p>未找到身份匹配的影像检查。</p>}
              {candidates.map((study) => (
                <div className="imaging-study" key={study.studyInstanceUid}>
                  <div>
                    <strong>{study.description || "未命名检查"}</strong>
                    <p>
                      {study.studyDate} · {study.modalities}
                    </p>
                    <small className="imaging-uid">
                      {study.studyInstanceUid}
                    </small>
                  </div>
                  <button
                    disabled={
                      busy ||
                      state.links.some(
                        (l) => l.studyInstanceUid === study.studyInstanceUid,
                      )
                    }
                    onClick={() =>
                      void act(async () => {
                        await api(root + "/links", {
                          studyInstanceUid: study.studyInstanceUid,
                        });
                        await refresh();
                        setNotice("影像已关联，已记录操作审计。");
                      })
                    }
                  >
                    {state.links.some(
                      (l) => l.studyInstanceUid === study.studyInstanceUid,
                    )
                      ? "已关联"
                      : "关联检查"}
                  </button>
                </div>
              ))}
            </div>
          )}
          <h4>已关联检查 · {state.links.length}</h4>
          {state.links.length === 0 && (
            <p className="muted">尚未关联影像，不影响原有预约流程。</p>
          )}
          {state.links.map((link) => (
            <div className="imaging-study" key={link.studyInstanceUid}>
              <div>
                <strong>{link.description || "影像检查"}</strong>
                <small className="imaging-uid">{link.studyInstanceUid}</small>
                <small>关联人：{link.linkedBy}</small>
              </div>
              <div className="imaging-actions">
                <button
                  disabled={busy}
                  onClick={() =>
                    void act(async () =>
                      setSelected({
                        uid: link.studyInstanceUid,
                        instances: await api<Instance[]>(
                          root + `/studies/${link.studyInstanceUid}/metadata`,
                        ),
                      }),
                    )
                  }
                >
                  查看序列
                </button>
                <a
                  className="link-button"
                  href={
                    root +
                    `/studies/${link.studyInstanceUid}/viewer/index.html?study=${link.studyInstanceUid}`
                  }
                  target="_blank"
                  rel="noreferrer"
                >
                  打开影像查看器 ↗
                </a>
                <button
                  disabled={busy}
                  onClick={() =>
                    void act(async () => {
                      await api(
                        root + `/links/${link.studyInstanceUid}/remove`,
                        {},
                      );
                      if (selected?.uid === link.studyInstanceUid)
                        setSelected(null);
                      await refresh();
                      setNotice("已解除关联，外部影像保留，操作记录已保存。");
                    })
                  }
                >
                  解除关联
                </button>
              </div>
            </div>
          ))}
          {selected && (
            <div className="imaging-series">
              <h4>序列与实例 · {selected.instances.length} 个实例</h4>
              {Array.from(
                new Set(selected.instances.map((i) => i.seriesInstanceUid)),
              ).map((series) => (
                <div key={series}>
                  <strong>
                    {selected.instances.find(
                      (i) => i.seriesInstanceUid === series,
                    )?.seriesDescription || "序列"}
                  </strong>
                  <small className="imaging-uid">{series}</small>
                  <div className="imaging-actions">
                    {selected.instances
                      .filter((i) => i.seriesInstanceUid === series)
                      .map((i) => (
                        <a
                          key={i.sopInstanceUid}
                          href={
                            root +
                            `/studies/${selected.uid}/series/${series}/instances/${i.sopInstanceUid}/file`
                          }
                        >
                          下载实例 {i.instanceNumber}（DICOM）
                        </a>
                      ))}
                  </div>
                </div>
              ))}
            </div>
          )}
          {state.audit.length > 0 && (
            <details>
              <summary>影像操作记录（最近30条）</summary>
              <ul>
                {state.audit.map((a) => (
                  <li key={a.id}>
                    {a.action === "Linked" ? "关联" : "解除关联"} · {a.actor} ·{" "}
                    {new Date(a.atUtc).toLocaleString("zh-CN")}
                    <small className="imaging-uid">{a.studyInstanceUid}</small>
                    <small>请求标识：{a.correlationId}</small>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </>
      )}
    </section>
  );
}
