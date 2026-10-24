General Idea:
    an ai agnostic plugin that aids the software lifescycle of teams. this should allow the team to approve a spec and allow for build automation.
    
asks:
    start with the base of anthropics native sdlc playbooks process. this has some changes to what anthropic recommends in its playbook
    each artifact is to be manually approved by a user.
    when a cycle skill is executed and the prior artifact does not exist or is not approved the the skill should fail to run and provide the warning
    batch-build should orchestrate the ability to execute multiple approved plans
    the plugin should have a strong bias on the tech stack it uses
    when creating pipelines lets make sure we build, test, display code coverage for the code for every pr.
        bare minimum is to display code coverage. no need to enforce a percentage of code ceverage

skills
    init
        initializes the project so that it has the correct shape for the sdlc plugin
    interogate-artifact
        will verify that the artifact passes all constraints and that it meets all criterea of its previous aritfact. i.e.: a spec matches its intent
    batch-build
    cycle
        intent
            interogate the user. identify if the change is on a new solution, a feature addition, feature change, bug-fix
        spec
            identify testable critera in the intent. define "what is done"
        plan
            review the spec and the existing code base. layout all necessary file changes needed to accomplish feature completion.
        build
            implement the plan, build, test. treats warnings as errors.
        verify
            reviews code changes, passing tests,
        pull-request
            attempt to create a pull request and link an issue or user story with the inten and spec details for a human to approve and for it to trigger ci/cd
        resolve-conflict
            resolve merge conflicts for batched builds that collide on claimed files
        
artifacts
    solution.md
        include the tech stack along with any solution specific constraints
    intent.md
        should include verbiage similar to a user story.
    spec
        should have testable criterea. things that can be tested and verified. this should allow to stop any looping by the tool
    plan
        should have file names(adding, updates, deletes). for updated files it should hold line adds and removes. basically file diffs
    build
        the actual code/file diffs in the feature branch
    verify
        a fail/pass verify.md
        
tech stack
    programming: language c#
    framework: .net
    testing: mstest
    ORM: ef core
    orchestration: aspire
        for emulation and running locally
    infrastructure as code: terraform
    database: ms sql
    cache: redis
    cloud: azure
    message bus: azure service bus/signal recommends
    web: maui(for web, desktop, android mobile)
        lets make sure we create proper abstraction so that projects are able to share common code
    api: asp.net core
    
folder structure
.sdlc
    cycles
        20260929_4e1a
