import { test, expect } from '@playwright/test';
test('A01 login and Apple-style workspace',async({page})=>{
 await page.goto('/');await expect(page.getByRole('heading',{name:'让预约，有条不紊。'})).toBeVisible();
 await page.getByLabel('密码',{exact:true}).fill(process.env.DEMO_PASSWORD!);
 await page.getByRole('button',{name:'进入工作台'}).click();
 await expect(page.getByRole('heading',{name:'预约工作台',exact:true})).toBeVisible();
 await expect(page.locator('.resource-card').getByText('预约室 A',{exact:true})).toBeVisible();
 await page.screenshot({path:'test-results/workspace.png',fullPage:true});
 await page.getByRole('button',{name:'退出登录'}).click();
 await expect(page.getByRole('heading',{name:'让预约，有条不紊。'})).toBeVisible();
});
test('A02 create, detail and actionable conflict',async({page})=>{
 await page.goto('/');await page.getByLabel('密码',{exact:true}).fill(process.env.DEMO_PASSWORD!);await page.getByRole('button',{name:'进入工作台'}).click();
 await page.getByRole('button',{name:'新建预约'}).click();
 const date=new Date(Date.UTC(2034,0,1)+Math.floor(Math.random()*100000)*900000).toISOString().slice(0,16);
 await page.getByLabel('开始时间（上海 UTC+8）').fill(date);
 await page.getByRole('button',{name:'创建预约',exact:true}).click();
 await expect(page.getByRole('heading',{name:'预约详情'})).toBeVisible();
 await expect(page.getByText('资料核对',{exact:false})).toBeVisible();
 await page.getByRole('button',{name:'关闭详情'}).click();
 await page.getByRole('button',{name:'新建预约'}).click();await page.getByLabel('开始时间（上海 UTC+8）').fill(date);await page.getByRole('button',{name:'创建预约',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('已被占用');
 await page.screenshot({path:'test-results/conflict.png',fullPage:true});
});
test('A06/A12 reschedule and cancel from detail',async({page})=>{
 await page.goto('/');await page.getByLabel('密码',{exact:true}).fill(process.env.DEMO_PASSWORD!);await page.getByRole('button',{name:'进入工作台'}).click();
 await page.getByRole('button',{name:'新建预约'}).click();
 const date=new Date(Date.UTC(2036,0,1)+Math.floor(Math.random()*100000)*900000).toISOString().slice(0,16);
 await page.getByLabel('开始时间（上海 UTC+8）').fill(date);await page.getByRole('button',{name:'创建预约',exact:true}).click();
 await page.getByRole('button',{name:'改期',exact:true}).click();
 await page.getByRole('combobox',{name:'预约资源',exact:true}).selectOption('2');await page.getByRole('button',{name:'保存改期'}).click();
 await expect(page.getByRole('heading',{name:'预约详情'})).toContainText('v2');
 page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'取消预约',exact:true}).click();
 await expect(page.getByRole('heading',{name:'预约详情'})).toContainText('已取消');
 await expect(page.getByRole('button',{name:'改期',exact:true})).toHaveCount(0);
});
