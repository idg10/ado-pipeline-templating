# Azure DevOps Pipeline Templating

Experiments with reuse in Azure DevOps (ADO) pipeline templating.

## Background

[endjin.com](https://endjin.com) defines various reusable ADO pipeline templates at https://github.com/endjin/Endjin.RecommendedPractices.AzureDevopsPipelines.GitHub
This repo contains experiments to see which techniques for reuse work, and which do not.

## Experiment: nesting step templates

It's possible to write an ADO template that defines extensibility points where particular applications of the template can add their own steps.
Is it possible for the steps we plug into those extensibility points to come from other templates?

This motivation originated from a requirement to add benchmarking to our build process (with the long term goal being to detect
performance regressions automatically). We don't want to force this on all projects, and we'd prefer not to have to maintain two versions
of our shared `build.and.release.yml` template that are identical save for this one step. The obvious way to avoid the problem of
maintaining multiple variations on a single theme is to have one standard build and release template with some placeholders in
which users of the template can supply optional extra steps.

But what if several projects are all going to want to do the same optional extra steps? If we define a set of steps that, say, run a
BenchmarkDotNet benchmark and then publish the results as part of the build artifacts, we'd want to be able to reuse those across
multiple projects.

This is possible, and the templates in this repo show one way to do it.

The reusable template that performs the actual build is [templates/build.and.test.yaml](../master/templates/build.and.test.yaml). This is a very simplified
version of the template of the same name up at https://github.com/endjin/Endjin.RecommendedPractices.AzureDevopsPipelines.GitHub containing
just build and test steps and, critically, a placeholder:

``` yaml
  - ${{ parameters.postTestSteps }}
```

This `${{ ... }}`` syntax denotes a _template expression_. This is an expression that will be expanded at the point at which ADO produces
what the documentation sometimes calls a _plan_. In simple pipelines, the plan is conceptually indistinguishable from your pipeline
definition, but once you start using templates, it becomes an important concept: when the pipeline file you supply ADO with points to
one or more templates, ADO gathers them altogether and produces what you could think of as the actual build definition it's going to
run - what the build definition might have looked like if you weren't using templates. And it's at that point that it expands any
_template expressions_.

In this repo, the pipeline definition I've supplied to ADO is at [build-pipeline.yaml](../master/build-pipeline.yaml). This defines
a single job, but then defers entirely to a template to define the constituent parts of that job:

``` yaml
jobs:
- template: templates/build.test.and.benchmark.yml
  parameters:
    vmImage: 'ubuntu-latest'
    solution_to_build: $(Endjin_Solution_To_Build)
    benchmarkProjectName: 'TestBenchmark.Benchmark'
```

When ADO comes to generate the plan for this pipeline, it knows it will have to go and fetch the
[templates/build.test.and.benchmark.yaml](../master/templates/build.test.and.benchmark.yaml) template. That template requires us to tell it
certain things, such as the location of the solution file, and the name of the project that contains the benchmarks we'd like
to run as part of this build.

If you're following closely, you'll have noticed that this does _not_ refer to the `build.and.test.yaml` I showed a snippet from
earlier. It does use that, but it does so indirectly. And that's because the goal of the exercise here is to see if we can find
a reusable way to use reusable templates in certain ways. `build.and.test.yaml` is a reusable template that provides a general-purpose
placeholder for additional build steps; but we're using `build.test.and.benchmark.yml` which is another reusable template that exploits
that placeholder in a particular way, adding in benchmarking support.

But if you look at that template, it just invokes the `build.and.test.yaml` template from earlier, but, critically, the value it supplies
for the `postTestSteps` parameter is *another template*:

``` yaml
- template: build.and.test.yaml
  parameters:
    vmImage: ${{ parameters.vmImage }}
    solution_to_build: ${{ parameters.solution_to_build }}
    postTestSteps:
    - template: benchmark.yaml
      parameters:
        benchmarkProjectName: ${{ parameters.benchmarkProjectName }}
```

And this was the question I was attempting to answer with this experiment: when a template (like `build.and.test.yaml`) defines a
parameter that lets you supply a bunch of extra steps to run at a particular point, can those steps be sourced from some other
template? And the answer is: yes!

This means that I can define by `build.test.and.benchmark.yaml` by saying that it's basically the `build.and.test.yaml` template,
but with another template, [templates/benchmark.yaml](../master/templates/benchmark.yaml), plugged into the placeholder
offered by `build.and.test.yaml`.
