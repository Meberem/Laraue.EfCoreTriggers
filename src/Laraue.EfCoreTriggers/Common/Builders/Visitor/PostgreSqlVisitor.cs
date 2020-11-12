﻿using Laraue.Core.Extensions;
using Laraue.EfCoreTriggers.Common.Builders.Triggers.Base;
using Laraue.EfCoreTriggers.Common.Builders.Visitor;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Laraue.EfCoreTriggers.Common.Builders.Providers
{
    public class PostgreSqlVisitor : BaseTriggerSqlVisitor
    {
        public PostgreSqlVisitor(IModel model) : base(model)
        {
        }

        protected override string NewEntityPrefix => "NEW";

        protected override string OldEntityPrefix => "OLD";

        public override GeneratedSql GetDropTriggerSql(string triggerName, Type entityType)
        {
            return new GeneratedSql()
                .Append($"DROP TRIGGER {triggerName} ON {GetTableName(entityType)};")
                .Append($"DROP FUNCTION {triggerName}();");
        }

        public override GeneratedSql GetTriggerActionsSql<TTriggerEntity>(TriggerActions<TTriggerEntity> triggerActions)
        {
            var sqlResult = new GeneratedSql();

            if (triggerActions.ActionConditions.Count > 0)
            {
                var conditionsSql = triggerActions.ActionConditions.Select(actionCondition => actionCondition.BuildSql(this));
                sqlResult.MergeColumnsInfo(conditionsSql.SelectMany(x => x.AffectedColumns));
                sqlResult.Append($"IF ")
                    .AppendJoin(" AND ", conditionsSql.Select(x => x.SqlBuilder))
                    .Append($" THEN ");
            }

            var actionsSql = triggerActions.ActionExpressions.Select(action => action.BuildSql(this));
            sqlResult.MergeColumnsInfo(actionsSql.SelectMany(x => x.AffectedColumns))
                .AppendJoin(", ", actionsSql.Select(x => x.SqlBuilder));

            if (triggerActions.ActionConditions.Count > 0)
            {
                sqlResult
                    .Append($"END IF;");
            }

            return sqlResult;
        }

        public override GeneratedSql GetTriggerSql<TTriggerEntity>(Trigger<TTriggerEntity> trigger)
        {
            var actionsSql = trigger.Actions.Select(action => action.BuildSql(this));
            return new GeneratedSql(actionsSql.SelectMany(x => x.AffectedColumns))
                .Append($"CREATE FUNCTION {trigger.Name}() RETURNS trigger as ${trigger.Name}$ ")
                .Append("BEGIN ")
                .AppendJoin(actionsSql.Select(x => x.SqlBuilder))
                .Append(" RETURN NEW;END;")
                .Append($"${trigger.Name}$ LANGUAGE plpgsql;")
                .Append($"CREATE TRIGGER {trigger.Name} {trigger.TriggerTime.ToString().ToUpper()} {trigger.TriggerType.ToString().ToUpper()} ")
                .Append($"ON {GetTableName(typeof(TTriggerEntity))} FOR EACH ROW EXECUTE PROCEDURE {trigger.Name}();");
        }

        public override GeneratedSql GetTriggerUpsertActionSql<TTriggerEntity, TUpdateEntity>(TriggerUpsertAction<TTriggerEntity, TUpdateEntity> triggerUpsertAction)
        {
            var insertStatementSql = GetInsertStatementBodySql(triggerUpsertAction.InsertExpression, triggerUpsertAction.InsertExpressionPrefixes);
            var newExpressionColumnsSql = GetNewExpressionColumns((NewExpression)triggerUpsertAction.MatchExpression.Body);

            var sqlBuilder = new GeneratedSql(insertStatementSql.AffectedColumns)
                .MergeColumnsInfo(newExpressionColumnsSql.SelectMany(x => x.AffectedColumns))
                .Append($"INSERT INTO {GetTableName(typeof(TUpdateEntity))} ")
                .Append($" ON CONFLICT (")
                .AppendJoin(", ", newExpressionColumnsSql.Select(x => x.SqlBuilder))
                .Append(")");

            if (triggerUpsertAction.OnMatchExpression is null)
            {
                sqlBuilder.Append(" DO NOTHING");
            }
            else
            {
                var updateStatementSql = GetUpdateStatementBodySql(triggerUpsertAction.OnMatchExpression, triggerUpsertAction.OnMatchExpressionPrefixes);
                sqlBuilder.MergeColumnsInfo(updateStatementSql.AffectedColumns)
                    .Append($" DO UPDATE SET ")
                    .Append(updateStatementSql.SqlBuilder);
            }

            return sqlBuilder;
        }

        protected override GeneratedSql GetMethodConcatCallExpressionSql(params GeneratedSql[] concatExpressionArgsSql)
            => new GeneratedSql(concatExpressionArgsSql.SelectMany(x => x.AffectedColumns))
                .Append("CONCAT(")
                .AppendJoin(", ", concatExpressionArgsSql.Select(x => x.SqlBuilder))
                .Append(")");
    }
}
