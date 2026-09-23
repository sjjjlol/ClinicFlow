import { test, expect } from '@playwright/test';
test('A01 login and Apple-style workspace',async({page})=>{
 await page.goto('/');await expect(page.getByRole('heading',{name:'让预约，有条不紊。'})).toBeVisible();
 await page.getByLabel('密码',{exact:true}).fill(process.env.DEMO_PASSWORD!);
 await page.getByRole('button',{name:'进入工作台'}).click();
 await expect(page.getByRole('heading',{name:'预约工作台',exact:true})).toBeVisible();
 await expect(page.getByText('预约室 A',{exact:true})).toBeVisible();
 await page.screenshot({path:'test-results/workspace.png',fullPage:true});
 await page.getByRole('button',{name:'退出登录'}).click();
 await expect(page.getByRole('heading',{name:'让预约，有条不紊。'})).toBeVisible();
});
