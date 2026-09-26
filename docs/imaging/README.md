# 就诊前影像资料关联

本模块支持工作人员把患者已有的影像检查关联到门诊预约。预约不是检查申请，关联不意味着本次预约产生了该影像。使用合成几何图案，无真实患者数据。

## 四阶段交付

1. 环境与基础：Orthanc、Stone 查看器、可重复生成的数据及 STOW-RS 导入。
2. .NET 集成：DICOMweb 客户端、患者身份映射、关联关系、权限、审计与测试。
3. 页面与验证：预约详情入口、受控查看器、异常场景、CI。
4. 学习与面试：基础知识、代码导读、排障、演示、追问和证据。

每阶段分别本地提交；验证结果在交付证据中记录，不能把计划当作已实现。

## 环境启动

在仓库根目录执行 `./scripts/imaging-up.sh`。需要 Docker、Python 3 和首次安装依赖的网络。脚本只启动影像服务，不清空现有数据库。再次运行将导入相同 UID 的数据。

- Orthanc 镜像固定为 `orthancteam/orthanc:26.7.0`，开启 DICOMweb 和 Stone Web Viewer。
- HTTP 管理端口仅绑定回环地址 8042，启用 Basic 认证；用户名 `clinicflow`，密码取本地 `.env` 的 `INTEGRATION_TOKEN`，不提供给前端。
- 不开放 DIMSE 端口。本阶段实现 HTTP DICOMweb 集成。
- 数据保存到独立 Docker 卷 `imaging-data`；文件和 UID 清单生成到被 Git 忽略的 `artifacts/dicom-fixtures/`。
- 患者 1：`ClinicFlowDemo / CF-IMG-001`，两项检查；患者 2：`ClinicFlowDemo / CF-IMG-002`，一项检查。每项检查两个序列，每序列三个实例。
- 数据是 Secondary Capture、Explicit VR Little Endian、单帧 16 位灰度，不是 CT 扫描，也不能用作诊断。

## 标准与组件来源

- [DICOM PS3.18 Web Services](https://dicom.nema.org/medical/dicom/current/output/html/part18.html)：查询、获取、存储的标准语义。
- [DICOM PS3.10 文件格式](https://dicom.nema.org/medical/dicom/current/output/html/part10.html)：Part 10 文件封装。
- [Orthanc DICOMweb 插件](https://orthanc.uclouvain.be/book/plugins/dicomweb.html)：实际对接服务端。
- [Orthanc Docker 配置](https://orthanc.uclouvain.be/book/users/docker-orthancteam.html)：容器配置。
- [Stone 查看器](https://orthanc.uclouvain.be/book/plugins/stone-webviewer.html)：复用的影像显示组件。
- [pydicom 文件生成](https://pydicom.github.io/pydicom/stable/auto_examples/input_output/plot_write_dicom.html)：测试文件生成库。

标准接口与 Orthanc 私有 API 必须分清：fixture 通过 STOW-RS 导入；查看器静态资源来自 Orthanc 插件，它们本身不是 DICOM 协议。

### 第一阶段验证（2026-09-26）

Orthanc 容器健康检查通过；STOW-RS 成功导入 18 个实例；已实际获取 Stone 的入口与配置。macOS 当前 Python 的证书链需通过 `PIP_CERT=/etc/ssl/cert.pem ./scripts/imaging-up.sh` 指定系统 CA；没有关闭 TLS 校验。若其他环境证书正常，直接运行即可。
