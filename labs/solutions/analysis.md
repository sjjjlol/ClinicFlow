# 故障实验解析（完成练习后阅读）

L1：`./scripts/labs.sh L1 --fixed`。UPDATE加上`WHERE version=@readVersion`，更新时递增Version；affected=0表示冲突，需要调用方重新读取并决定。只有WHERE条件而没有递增，后续旧写入仍可能成功。正常预约还需要资源占用事务及锁协议，不能只加单行Version。

L2：`./scripts/labs.sh L2 --fixed`。唯一MessageId记录与副作用同事务；重复insert没有新效果。正常接收端还会比较单调预约版本，防止旧状态覆盖新状态。去重记录保留策略必须与最长重放周期一致；本练习不自动清理。重复HTTP本身无法完全消除，网络超时表示结果未知。

L3：组合索引`(resource_id,start_utc,id)`与等值筛选、时间范围和稳定排序一致，减少扫描和排序。COUNT、不同筛选组合和深offset分页可能需要不同方案；索引也增加写入与空间成本。实验结果见实际证据，计时受缓存和本机负载影响，不包装为生产指标。
