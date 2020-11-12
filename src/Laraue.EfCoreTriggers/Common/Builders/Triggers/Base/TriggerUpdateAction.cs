﻿using Laraue.EfCoreTriggers.Common.Builders.Visitor;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Laraue.EfCoreTriggers.Common.Builders.Triggers.Base
{
    public abstract class TriggerUpdateAction<TTriggerEntity, TUpdateEntity> : ITriggerAction
       where TTriggerEntity : class
       where TUpdateEntity : class
    {
        internal LambdaExpression UpdateFilter;
        internal LambdaExpression UpdateExpression;

        public TriggerUpdateAction(
            LambdaExpression updateFilter,
            LambdaExpression updateExpression)
        {
            UpdateFilter = updateFilter;
            UpdateExpression = updateExpression;
        }

        public virtual GeneratedSql BuildSql(ITriggerSqlVisitor visitor)
            => visitor.GetTriggerUpdateActionSql(this);

        internal abstract Dictionary<string, ArgumentPrefix> UpdateFilterPrefixes { get; }

        internal abstract Dictionary<string, ArgumentPrefix> UpdateExpressionPrefixes { get; }
    }
}