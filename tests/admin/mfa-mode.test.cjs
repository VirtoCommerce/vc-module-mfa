const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { createRequire } = require("node:module");
const { test } = require("node:test");

const root = path.resolve(__dirname, "../..");
const web = path.join(root, "src/VirtoCommerce.MFA.Web");
const vueRequire = createRequire(path.join(web, "vue/package.json"));
const vue = vueRequire("vue");
const ts = vueRequire("typescript");

function backend() {
    const state = { mode: "Off", isExempt: false, failStatus: false, failExemption: false, failModeRead: false, holdStatus: false, writes: [] };
    return {
        state,
        pendingStatus: [],
        status: () => {
            if (state.failStatus) throw new Error("Status unavailable");
            return { isGloballyEnabled: true, isBackofficeExempt: state.isExempt, isRequiredForBackoffice: !state.isExempt, stores: [] };
        }
    };
}

function angularController(server, subjectType = "user", canManage = true) {
    let controller;
    const scope = { blade: { subjectId: "user-1", subjectType }, $watch() {} };
    const readMode = (_args, done, fail) => {
        if (server.state.failModeRead) { fail(); return; }
        done({ mode: server.state.mode });
    };
    const writeMode = (_args, body, done) => {
        server.state.writes.push(["mode", body.mode]);
        server.state.mode = body.mode;
        done({ mode: body.mode });
    };
    const api = {
        getUserMode: readMode,
        getOrganizationMode: readMode,
        setUserMode: writeMode,
        setOrganizationMode: writeMode,
        getBackofficeExemption: (_args, done) => done({ isExempt: server.state.isExempt }),
        setBackofficeExemption: (_args, body, done, fail) => {
            if (server.state.failExemption) { fail(); return; }
            server.state.writes.push(["exemption", body.isExempt]);
            server.state.isExempt = body.isExempt;
            done({ isExempt: body.isExempt });
        },
        getUserStatus: (_args, done) => {
            if (server.state.failStatus) return;
            const snapshot = server.status();
            if (server.state.holdStatus) server.pendingStatus.push(() => done(snapshot));
            else done(snapshot);
        },
        getOrganizationStatus: (_args, done) => { if (!server.state.failStatus) done(server.status()); }
    };
    vm.runInNewContext(fs.readFileSync(path.join(web, "Scripts/blades/mfa-mode.js"), "utf8"), {
        angular: { module: () => ({ controller: (_name, definition) => { controller = definition.at(-1); } }) }
    });
    controller(scope, api, { checkPermission: () => canManage });
    return scope;
}

async function vueController(server) {
    const source = fs.readFileSync(path.join(web, "vue/src/modules/pages/mode.vue"), "utf8").match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1];
    const api = {
        MFA_MODES: ["Off", "Enabled", "Required"],
        getMode: async () => {
            if (server.state.failModeRead) throw new Error("Mode unavailable");
            return server.state.mode;
        },
        setMode: async (_type, _id, mode) => {
            server.state.writes.push(["mode", mode]);
            return server.state.mode = mode;
        },
        getBackofficeExemption: async () => server.state.isExempt,
        setBackofficeExemption: async (_id, isExempt) => {
            if (server.state.failExemption) throw new Error("Save failed");
            server.state.writes.push(["exemption", isExempt]);
            return server.state.isExempt = isExempt;
        },
        getStatus: async () => {
            const snapshot = server.status();
            if (server.state.holdStatus) return new Promise(resolve => server.pendingStatus.push(() => resolve(snapshot)));
            return snapshot;
        }
    };
    const context = vm.createContext({
        exports: {}, defineBlade() {},
        require(name) {
            if (name === "vue") return vue;
            if (name === "@vc-shell/framework") return {
                useBlade: () => ({ options: vue.ref({ subjectType: "user", subjectId: "user-1" }) }),
                usePermissions: () => ({ hasAccess: () => true })
            };
            if (name === "vue-i18n") return { useI18n: () => ({ t: key => key }) };
            if (name === "../api") return api;
            throw new Error("Unexpected import: " + name);
        }
    });
    const output = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
    vm.runInContext(output, context);
    await vm.runInContext("load()", context);
    await vue.nextTick();
    return expression => vm.runInContext(expression, context);
}

test("Angular: saving only an exemption preserves a concurrent storefront mode change", () => {
    const server = backend();
    const scope = angularController(server);
    server.state.mode = "Required";
    scope.blade.backofficeExempt = true;
    scope.blade.toolbarCommands[0].executeMethod();
    assert.deepEqual(server.state.writes, [["exemption", true]]);
    assert.equal(server.state.mode, "Required");
    assert.equal(scope.blade.mode, "Required");
    assert.equal(scope.blade.status.isRequiredForBackoffice, false);
});

test("Angular: a status failure after saving never leaves the old requirement visible", () => {
    const server = backend();
    const scope = angularController(server);
    assert.equal(scope.blade.status.isRequiredForBackoffice, true);
    server.state.failStatus = true;
    scope.blade.backofficeExempt = true;
    scope.blade.toolbarCommands[0].executeMethod();
    assert.equal(server.state.isExempt, true);
    assert.equal(scope.isExemptionDirty(), false);
    assert.equal(scope.blade.status, null);
});

test("Angular: a failed exemption save stays dirty and can be reset", () => {
    const server = backend();
    const scope = angularController(server);
    server.state.failExemption = true;
    scope.blade.backofficeExempt = true;
    scope.blade.toolbarCommands[0].executeMethod();
    assert.equal(scope.isExemptionDirty(), true);
    assert.equal(scope.blade.isLoading, false);
    scope.blade.toolbarCommands[1].executeMethod();
    assert.equal(scope.isDirty(), false);
    assert.equal(scope.blade.backofficeExempt, false);
});

test("Angular: saving an organization mode never writes a user exemption", () => {
    const server = backend();
    const scope = angularController(server, "organization");
    scope.blade.mode = "Required";
    scope.blade.backofficeExempt = true;
    scope.blade.toolbarCommands[0].executeMethod();
    assert.deepEqual(server.state.writes, [["mode", "Required"]]);
    assert.equal(server.state.isExempt, false);
});

test("Angular: a Disabled store names the exemption instead of claiming global MFA is off", () => {
    const server = backend();
    server.state.isExempt = true;
    const scope = angularController(server);
    assert.equal(scope.getReasonKey({ storeMode: "Disabled", isDisabledModeSkipped: true, isRequired: false }),
        "mfa.blades.mode.status.reasons.BackofficeExempt");
});

test("Angular: saving invalidates a pending status response even when reloading the user fails", () => {
    const server = backend();
    server.state.holdStatus = true;
    const scope = angularController(server);
    server.state.failModeRead = true;
    scope.blade.backofficeExempt = true;
    scope.blade.toolbarCommands[0].executeMethod();
    server.pendingStatus.forEach(deliver => deliver());
    assert.equal(server.state.isExempt, true);
    assert.equal(scope.isDirty(), false);
    assert.equal(scope.blade.status, null);
});

test("Vue: saving invalidates a pending status response even when reloading the user fails", async () => {
    const server = backend();
    server.state.holdStatus = true;
    const run = await vueController(server);
    server.state.failModeRead = true;
    run("backofficeExempt.value = true");
    await assert.rejects(run("bladeToolbar.value[0].clickHandler()"), /Mode unavailable/);
    server.pendingStatus.forEach(deliver => deliver());
    await vue.nextTick();
    assert.equal(server.state.isExempt, true);
    assert.equal(run("isDirty.value"), false);
    assert.equal(run("status.value"), undefined);
});

test("Vue: saving only an exemption preserves a concurrent storefront mode change", async () => {
    const server = backend();
    const run = await vueController(server);
    server.state.mode = "Required";
    run("backofficeExempt.value = true");
    await run("bladeToolbar.value[0].clickHandler()");
    assert.deepEqual(server.state.writes, [["exemption", true]]);
    assert.equal(run("mode.value"), "Required");
    assert.equal(run("status.value.isRequiredForBackoffice"), false);
});

test("Vue: a failed status read after saving clears the old requirement", async () => {
    const server = backend();
    const run = await vueController(server);
    server.state.failStatus = true;
    run("backofficeExempt.value = true");
    await run("bladeToolbar.value[0].clickHandler()");
    assert.equal(server.state.isExempt, true);
    assert.equal(run("status.value"), undefined);
});

test("Vue: a failed exemption save stays dirty and can be reset", async () => {
    const server = backend();
    const run = await vueController(server);
    server.state.failExemption = true;
    run("backofficeExempt.value = true");
    await assert.rejects(run("bladeToolbar.value[0].clickHandler()"), /Save failed/);
    assert.equal(run("isExemptionDirty.value"), true);
    assert.equal(run("loading.value"), false);
    run("bladeToolbar.value[1].clickHandler()");
    assert.equal(run("isDirty.value"), false);
});
